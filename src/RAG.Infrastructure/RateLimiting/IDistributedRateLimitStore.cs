namespace RAG.Infrastructure.RateLimiting;

/// <summary>
/// Interface for distributed rate limit storage.
/// This interface defines the contract for implementing distributed rate limiting
/// across multiple application instances.
///
/// MVP Implementation: Uses in-memory storage (single instance deployment)
/// Production Scaling: Implement with Redis using StackExchange.Redis
///
/// SCALING CONSIDERATIONS:
/// - Single Instance: MemoryCache (current implementation) works perfectly
/// - Multiple Instances: Rate limits are not shared between instances
/// - Solution: Implement this interface with Redis for distributed counting
///
/// IMPLEMENTATION NOTES:
/// When migrating to distributed rate limiting:
/// 1. Add Microsoft.Extensions.Caching.StackExchangeRedis package
/// 2. Implement this interface with Redis hash operations
/// 3. Use atomic INCR operations for thread-safe counting
/// 4. Set key expiration matching the rate limit window
/// </summary>
public interface IDistributedRateLimitStore
{
    /// <summary>
    /// Increments the request counter for a client and returns the new count.
    /// </summary>
    /// <param name="clientId">The client identifier (IP or user ID)</param>
    /// <param name="endpoint">The endpoint being accessed</param>
    /// <param name="windowDuration">Duration of the rate limit window</param>
    /// <returns>Current request count for this window</returns>
    Task<long> IncrementAsync(string clientId, string endpoint, TimeSpan windowDuration);

    /// <summary>
    /// Gets the current request count for a client.
    /// </summary>
    Task<long> GetCountAsync(string clientId, string endpoint);

    /// <summary>
    /// Gets the time remaining until the rate limit window resets.
    /// </summary>
    Task<TimeSpan?> GetResetTimeAsync(string clientId, string endpoint);

    /// <summary>
    /// Resets the counter for a client (for testing purposes).
    /// </summary>
    Task ResetAsync(string clientId, string endpoint);
}

// NOTE: Redis implementation example (post-MVP):
//
// public class RedisRateLimitStore : IDistributedRateLimitStore
// {
//     private readonly IConnectionMultiplexer _redis;
//
//     public async Task<long> IncrementAsync(string clientId, string endpoint, TimeSpan windowDuration)
//     {
//         var db = _redis.GetDatabase();
//         var key = $"ratelimit:{clientId}:{endpoint}";
//         var count = await db.StringIncrementAsync(key);
//         if (count == 1)
//         {
//             await db.KeyExpireAsync(key, windowDuration);
//         }
//         return count;
//     }
// }
