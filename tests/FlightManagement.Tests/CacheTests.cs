using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlightManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Moq;
using StackExchange.Redis;

namespace FlightManagement.Tests;

public sealed class CacheTests(FlightHost host) : IClassFixture<FlightHost>
{
    private ConcurrentDictionary<string, RedisValue> MemoryCache()
    {
        var values = new ConcurrentDictionary<string, RedisValue>();
        host.Redis.Setup(r => r.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) => values.TryGetValue(key.ToString(), out var value) ? value : RedisValue.Null);
        host.Redis.Setup(r => r.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, RedisValue value, TimeSpan? ttl, bool _, When __, CommandFlags ___) =>
            {
                Assert.Equal(TimeSpan.FromMinutes(5), ttl);
                values[key.ToString()] = value;
                return true;
            });
        return values;
    }

    [Fact]
    public async Task CacheHitMiss_UsesCachedScheduleAndNeverContainsPrivateCollections()
    {
        var values = MemoryCache();
        using var account = await host.RegisterAndLogin();
        var passenger = await host.Passenger(account);
        var flight = await host.Flight();
        Assert.Equal(HttpStatusCode.Created,
            (await account.Api.PostAsJsonAsync("/api/Tickets", FlightHost.Booking(flight, passenger))).StatusCode);
        using var client = host.NewApiClient();
        host.Redis.Invocations.Clear();
        var first = await client.GetStringAsync("/api/Flights");
        var second = await client.GetStringAsync("/api/Flights");
        Assert.Equal(first, second);
        Assert.Contains(flight.FlightNumber, first);
        using var json = JsonDocument.Parse(first);
        Assert.All(json.RootElement.EnumerateArray(), row =>
        {
            Assert.False(row.TryGetProperty("tickets", out _));
            Assert.False(row.TryGetProperty("crewAssignments", out _));
        });
        Assert.DoesNotContain("TEST-ONLY", string.Join("", values.Values));
        Assert.DoesNotContain(passenger.Email, string.Join("", values.Values));
        host.Redis.Verify(r => r.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Theory]
    [InlineData("read-connection")]
    [InlineData("read-timeout")]
    [InlineData("write")]
    [InlineData("corrupt")]
    [InlineData("null-json")]
    public async Task CacheCorruptionAndOutage_ReadsAuthoritativeDatabase(string failure)
    {
        MemoryCache();
        var flight = await host.Flight();
        if (failure == "read-connection")
            host.Redis.Setup(r => r.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "Synthetic outage"));
        if (failure == "read-timeout")
            host.Redis.Setup(r => r.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ThrowsAsync(new TimeoutException("Synthetic timeout"));
        if (failure is "corrupt" or "null-json")
            host.Redis.Setup(r => r.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisValue)(failure == "corrupt" ? "broken-json" : "null"));
        if (failure == "write")
            host.Redis.Setup(r => r.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "Synthetic outage"));
        using var client = host.NewApiClient();
        var result = await client.GetFromJsonAsync<Flight>($"/api/Flights/{flight.Id}");
        Assert.Equal(flight.FlightNumber, result!.FlightNumber);
        Assert.Equal(flight.TotalSeats, result.TotalSeats);
    }

    [Fact]
    public async Task BookingUpdateAndCancellation_AdvanceRevision_OnlyOnCommittedChanges()
    {
        MemoryCache();
        using var account = await host.RegisterAndLogin();
        var passenger = await host.Passenger(account);
        var flight = await host.Flight(1);
        await using var db = host.Db();
        async Task<long> Revision() => await db.CacheRevisions.AsNoTracking().Select(r => r.Version).SingleAsync();
        long before = await Revision();
        var created = await account.Api.PostAsJsonAsync("/api/Tickets", FlightHost.Booking(flight, passenger));
        var ticket = (await created.Content.ReadFromJsonAsync<Ticket>())!;
        Assert.Equal(before + 1, await Revision());
        Assert.Equal(HttpStatusCode.Conflict,
            (await account.Api.PostAsJsonAsync("/api/Tickets", FlightHost.Booking(flight, passenger, "2B"))).StatusCode);
        Assert.Equal(before + 1, await Revision());
        Assert.Equal(HttpStatusCode.NoContent, (await account.Api.PutAsJsonAsync($"/api/Tickets/{ticket.Id}", new {
            ticket.Id, ticket.FlightId, ticket.PassengerId, SeatNumber = "3C", Price = 1500m })).StatusCode);
        Assert.Equal(before + 2, await Revision());
        Assert.Equal(HttpStatusCode.NoContent, (await account.Api.DeleteAsync($"/api/Tickets/{ticket.Id}")).StatusCode);
        Assert.Equal(before + 3, await Revision());
    }

    [Fact]
    public async Task LateCacheFill_CannotResurrectScheduleAfterCommittedUpdate()
    {
        var values = MemoryCache();
        using var admin = await host.RegisterAndLogin();
        await host.MakeAdmin(admin);
        var flight = await host.Flight();
        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool first = true;
        host.Redis.Setup(r => r.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Returns(async (RedisKey key, RedisValue value, TimeSpan? ttl, bool _, When __, CommandFlags ___) =>
            {
                if (first)
                {
                    first = false;
                    arrived.SetResult();
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(15));
                }
                values[key.ToString()] = value;
                return true;
            });
        using var client = host.NewApiClient();
        var staleRead = client.GetAsync($"/api/Flights/{flight.Id}");
        await arrived.Task.WaitAsync(TimeSpan.FromSeconds(15));
        try
        {
            flight.Status = "Delayed";
            Assert.Equal(HttpStatusCode.NoContent,
                (await admin.Api.PutAsJsonAsync($"/api/Flights/{flight.Id}", flight)).StatusCode);
        }
        finally { release.SetResult(); }
        Assert.Equal(HttpStatusCode.OK, (await staleRead).StatusCode);
        var current = await client.GetFromJsonAsync<Flight>($"/api/Flights/{flight.Id}");
        Assert.Equal("Delayed", current!.Status);
        Assert.Equal(2, values.Count); // old and new revisions; only new is reachable.
    }

    [Fact]
    public async Task FlightCreateAndDelete_InvalidateListAndDetailByDatabaseRevision()
    {
        MemoryCache();
        using var admin = await host.RegisterAndLogin();
        await host.MakeAdmin(admin);
        using var client = host.NewApiClient();
        var before = (await client.GetFromJsonAsync<List<Flight>>("/api/Flights"))!;
        var input = new Flight { FlightNumber = "CACHE-TEST", Origin = "Test", Destination = "Test",
            TotalSeats = 10, DepartureTime = DateTime.UtcNow.AddDays(1), ArrivalTime = DateTime.UtcNow.AddDays(1).AddHours(1) };
        var response = await admin.Api.PostAsJsonAsync("/api/Flights", input);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<Flight>())!;
        Assert.Equal(before.Count + 1, (await client.GetFromJsonAsync<List<Flight>>("/api/Flights"))!.Count);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/Flights/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.Api.DeleteAsync($"/api/Flights/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/Flights/{created.Id}")).StatusCode);
        Assert.DoesNotContain((await client.GetFromJsonAsync<List<Flight>>("/api/Flights"))!, f => f.Id == created.Id);
    }
}
