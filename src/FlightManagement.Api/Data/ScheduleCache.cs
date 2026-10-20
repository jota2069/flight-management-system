using System.Net.Sockets;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace FlightManagement.Api.Data;

// Redis is optional acceleration, never the authority for identity, seats or freshness.
public sealed class ScheduleCache(AppDbContext db, Lazy<IConnectionMultiplexer> connection,
    ILogger<ScheduleCache> logger)
{
    public async Task<T?> Read<T>(string scope, Func<Task<T?>> load) where T : class
    {
        // A committed mutation advances this version in the SAME PostgreSQL transaction.
        // A late cache fill can only populate the old key, never resurrect stale data.
        long version = await db.CacheRevisions.Where(r => r.Id == 1).Select(r => r.Version).SingleAsync();
        string key = $"schedule:v3:{version}:{scope}";
        try
        {
            var value = await connection.Value.GetDatabase().StringGetAsync(key);
            if (!value.IsNullOrEmpty)
            {
                var cached = JsonSerializer.Deserialize<T>(value.ToString());
                if (cached is not null) return cached;
            }
        }
        catch (Exception e) when (CacheFailure(e) || e is JsonException)
        {
            logger.LogWarning("Schedule cache read unavailable/invalid; using PostgreSQL.");
        }
        var result = await load();
        if (result is not null)
        {
            try
            {
                await connection.Value.GetDatabase().StringSetAsync(key,
                    JsonSerializer.Serialize(result), TimeSpan.FromMinutes(5), false, When.Always);
            }
            catch (Exception e) when (CacheFailure(e))
            {
                logger.LogWarning("Schedule cache write unavailable; PostgreSQL result retained.");
            }
        }
        return result;
    }

    public static Task Advance(AppDbContext db)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Cache revision must be changed inside a business transaction.");
        return db.Database.ExecuteSqlRawAsync(
            """UPDATE "CacheRevisions" SET "Version" = "Version" + 1 WHERE "Id" = 1""");
    }

    private static bool CacheFailure(Exception e) =>
        e is RedisException or TimeoutException or SocketException;
}
