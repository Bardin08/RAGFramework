using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Application.Interfaces;
using RAG.Core.Configuration;

namespace RAG.Infrastructure.Services;

/// <summary>
/// User lookup service that integrates with Keycloak Admin API.
/// </summary>
public class KeycloakUserLookupService : IUserLookupService
{
    private readonly HttpClient _httpClient;
    private readonly KeycloakAdminSettings _settings;
    private readonly IMemoryCache _cache;
    private readonly ILogger<KeycloakUserLookupService> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public KeycloakUserLookupService(
        HttpClient httpClient,
        IOptions<KeycloakSettings> keycloakSettings,
        IMemoryCache cache,
        ILogger<KeycloakUserLookupService> logger)
    {
        _httpClient = httpClient;
        _settings = keycloakSettings.Value.Admin;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<UserInfo?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogDebug("User lookup is disabled");
            return null;
        }

        var cacheKey = $"user:{userId}";
        if (_cache.TryGetValue(cacheKey, out UserInfo? cachedUser))
        {
            _logger.LogDebug("User {UserId} found in cache", userId);
            return cachedUser;
        }

        try
        {
            var token = await GetAccessTokenAsync(cancellationToken);
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Failed to obtain admin access token");
                return null;
            }

            var url = $"{_settings.BaseUrl}/admin/realms/{_settings.Realm}/users/{userId}";
            
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogDebug("User {UserId} not found in Keycloak", userId);
                    return null;
                }

                _logger.LogWarning("Failed to get user {UserId} from Keycloak: {StatusCode}", 
                    userId, response.StatusCode);
                return null;
            }

            var keycloakUser = await response.Content.ReadFromJsonAsync<KeycloakUser>(JsonOptions, cancellationToken);
            if (keycloakUser == null)
            {
                return null;
            }

            var userInfo = MapToUserInfo(keycloakUser);
            
            // Cache the result
            _cache.Set(cacheKey, userInfo, TimeSpan.FromSeconds(_settings.CacheDurationSeconds));
            
            _logger.LogDebug("Retrieved user {UserId} ({Username}) from Keycloak", userId, userInfo.Username);
            return userInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up user {UserId} from Keycloak", userId);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<Dictionary<Guid, UserInfo>> GetUsersByIdsAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<Guid, UserInfo>();
        var idsToFetch = new List<Guid>();

        // Check cache first
        foreach (var userId in userIds.Distinct())
        {
            var cacheKey = $"user:{userId}";
            if (_cache.TryGetValue(cacheKey, out UserInfo? cachedUser) && cachedUser != null)
            {
                result[userId] = cachedUser;
            }
            else
            {
                idsToFetch.Add(userId);
            }
        }

        // Fetch remaining users
        foreach (var userId in idsToFetch)
        {
            var user = await GetUserByIdAsync(userId, cancellationToken);
            if (user != null)
            {
                result[userId] = user;
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UserInfo>> SearchUsersAsync(
        Guid tenantId,
        string searchTerm,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogDebug("User search is disabled");
            return Array.Empty<UserInfo>();
        }

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return Array.Empty<UserInfo>();
        }

        try
        {
            var token = await GetAccessTokenAsync(cancellationToken);
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Failed to obtain admin access token for user search");
                return Array.Empty<UserInfo>();
            }

            // Search by email or username prefix
            var url = $"{_settings.BaseUrl}/admin/realms/{_settings.Realm}/users?search={Uri.EscapeDataString(searchTerm)}&max={maxResults}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to search users in Keycloak: {StatusCode}", response.StatusCode);
                return Array.Empty<UserInfo>();
            }

            var keycloakUsers = await response.Content.ReadFromJsonAsync<List<KeycloakUser>>(JsonOptions, cancellationToken);
            if (keycloakUsers == null || keycloakUsers.Count == 0)
            {
                return Array.Empty<UserInfo>();
            }

            // Filter by tenant if tenant attribute is present
            var filteredUsers = keycloakUsers
                .Where(u => u.Enabled)
                .Select(MapToUserInfo)
                .ToList();

            _logger.LogDebug("Found {Count} users matching '{SearchTerm}'", filteredUsers.Count, searchTerm);
            return filteredUsers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching users in Keycloak");
            return Array.Empty<UserInfo>();
        }
    }

    private async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        // Check if current token is still valid
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry.AddMinutes(-1))
        {
            return _accessToken;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry.AddMinutes(-1))
            {
                return _accessToken;
            }

            var tokenUrl = $"{_settings.BaseUrl}/realms/{_settings.Realm}/protocol/openid-connect/token";
            
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _settings.ClientId,
                ["client_secret"] = _settings.ClientSecret
            });

            var response = await _httpClient.PostAsync(tokenUrl, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to get Keycloak admin token: {StatusCode} - {Error}", 
                    response.StatusCode, error);
                return null;
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, cancellationToken);
            if (tokenResponse == null)
            {
                return null;
            }

            _accessToken = tokenResponse.AccessToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
            
            _logger.LogDebug("Obtained new Keycloak admin token, expires in {ExpiresIn}s", tokenResponse.ExpiresIn);
            return _accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static UserInfo MapToUserInfo(KeycloakUser keycloakUser)
    {
        return new UserInfo
        {
            Id = Guid.TryParse(keycloakUser.Id, out var id) ? id : Guid.Empty,
            Username = keycloakUser.Username ?? string.Empty,
            Email = keycloakUser.Email,
            FirstName = keycloakUser.FirstName,
            LastName = keycloakUser.LastName
        };
    }

    private class KeycloakUser
    {
        public string? Id { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public bool Enabled { get; set; }
        public bool EmailVerified { get; set; }
    }

    private class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;
    }
}
