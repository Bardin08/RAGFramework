using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Core.Authorization;
using RAG.Core.Domain.Enums;

namespace RAG.Infrastructure.Services;

/// <summary>
/// Service for checking document access permissions with caching.
/// Permission check flow:
/// 1. Admin role → full access
/// 2. Owner → full access
/// 3. Explicit permission in document_access table
/// 4. Public document + same tenant → read access
/// 5. Otherwise → denied
/// </summary>
public class DocumentPermissionService : IDocumentPermissionService
{
    private readonly IDocumentAccessRepository _accessRepository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DocumentPermissionService> _logger;

    private const string CacheKeyPrefix = "permission";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const string TenantIdClaimType = "tenant_id";

    public DocumentPermissionService(
        IDocumentAccessRepository accessRepository,
        IMemoryCache cache,
        ILogger<DocumentPermissionService> logger)
    {
        _accessRepository = accessRepository ?? throw new ArgumentNullException(nameof(accessRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> CanAccessAsync(
        Guid documentId,
        ClaimsPrincipal user,
        PermissionType requiredPermission,
        CancellationToken cancellationToken = default)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? user.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            _logger.LogDebug("Access denied: No valid user ID in claims for document {DocumentId}", documentId);
            return false;
        }

        // 1. Admin bypass
        if (user.IsInRole(ApplicationRoles.Admin))
        {
            _logger.LogDebug(
                "Admin bypass: User {UserId} granted {Permission} access to document {DocumentId}",
                userId, requiredPermission, documentId);
            return true;
        }

        // Get document to check ownership and public status
        var document = await _accessRepository.GetDocumentWithOwnerAsync(documentId, cancellationToken);
        if (document == null)
        {
            _logger.LogDebug("Access denied: Document {DocumentId} not found", documentId);
            return false;
        }

        // Extract tenant from claims
        var tenantIdClaim = user.FindFirst(TenantIdClaimType)?.Value;
        if (string.IsNullOrEmpty(tenantIdClaim) || !Guid.TryParse(tenantIdClaim, out var userTenantId))
        {
            _logger.LogDebug("Access denied: No valid tenant ID in claims for document {DocumentId}", documentId);
            return false;
        }

        // Verify tenant isolation
        if (document.TenantId != userTenantId)
        {
            _logger.LogWarning(
                "Cross-tenant access attempt: User {UserId} (tenant {UserTenant}) tried to access document {DocumentId} (tenant {DocTenant})",
                userId, userTenantId, documentId, document.TenantId);
            return false;
        }

        // 2. Owner has full access
        if (document.OwnerId == userId)
        {
            _logger.LogDebug(
                "Owner access: User {UserId} granted {Permission} access to owned document {DocumentId}",
                userId, requiredPermission, documentId);
            return true;
        }

        // Check cache first
        var cacheKey = GetCacheKey(documentId, userId);
        if (_cache.TryGetValue<PermissionType?>(cacheKey, out var cachedPermission))
        {
            _logger.LogDebug(
                "Cache hit for permission check: User {UserId}, Document {DocumentId}, Permission {Permission}",
                userId, documentId, cachedPermission);

            if (cachedPermission.HasValue && HasSufficientPermission(cachedPermission.Value, requiredPermission))
            {
                return true;
            }
        }

        // 3. Check explicit permission
        var access = await _accessRepository.GetAccessAsync(documentId, userId, cancellationToken);
        PermissionType? effectivePermission = access?.Permission;

        // Cache the result
        _cache.Set(cacheKey, effectivePermission, CacheDuration);

        if (access != null && access.HasPermission(requiredPermission))
        {
            _logger.LogDebug(
                "Explicit permission: User {UserId} has {Granted} (required {Required}) for document {DocumentId}",
                userId, access.Permission, requiredPermission, documentId);
            return true;
        }

        // 4. Public document - allow read access for same-tenant users
        if (document.IsPublic && requiredPermission == PermissionType.Read)
        {
            _logger.LogDebug(
                "Public access: User {UserId} granted read access to public document {DocumentId}",
                userId, documentId);
            return true;
        }

        _logger.LogDebug(
            "Access denied: User {UserId} lacks {Required} permission for document {DocumentId}",
            userId, requiredPermission, documentId);
        return false;
    }

    /// <inheritdoc />
    public async Task<PermissionType?> GetEffectivePermissionAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cacheKey = GetCacheKey(documentId, userId);
        if (_cache.TryGetValue<PermissionType?>(cacheKey, out var cachedPermission))
        {
            return cachedPermission;
        }

        // Get document to check ownership
        var document = await _accessRepository.GetDocumentWithOwnerAsync(documentId, cancellationToken);
        if (document == null)
        {
            return null;
        }

        // Owner has admin permission
        if (document.OwnerId == userId)
        {
            var ownerPermission = PermissionType.Admin;
            _cache.Set(cacheKey, (PermissionType?)ownerPermission, CacheDuration);
            return ownerPermission;
        }

        // Check explicit permission
        var access = await _accessRepository.GetAccessAsync(documentId, userId, cancellationToken);
        PermissionType? effectivePermission = access?.Permission;

        // Cache the result (even if null)
        _cache.Set(cacheKey, effectivePermission, CacheDuration);

        return effectivePermission;
    }

    /// <inheritdoc />
    public void InvalidateCache(Guid documentId, Guid userId)
    {
        var cacheKey = GetCacheKey(documentId, userId);
        _cache.Remove(cacheKey);
        _logger.LogDebug("Invalidated permission cache: User {UserId}, Document {DocumentId}", userId, documentId);
    }

    /// <inheritdoc />
    public void InvalidateCacheForDocument(Guid documentId)
    {
        // Note: IMemoryCache doesn't support wildcard removal
        // In production, consider using IDistributedCache with key patterns
        // For now, this is a no-op - entries will expire naturally
        _logger.LogDebug("Document cache invalidation requested for {DocumentId} (TTL-based expiry)", documentId);
    }

    private static string GetCacheKey(Guid documentId, Guid userId)
    {
        return $"{CacheKeyPrefix}:{userId}:{documentId}";
    }

    private static bool HasSufficientPermission(PermissionType granted, PermissionType required)
    {
        return granted switch
        {
            PermissionType.Admin => true,
            PermissionType.Write => required <= PermissionType.Write,
            PermissionType.Read => required == PermissionType.Read,
            _ => false
        };
    }
}
