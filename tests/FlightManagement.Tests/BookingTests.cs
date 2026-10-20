using System.Net;
using System.Net.Http.Json;
using FlightManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FlightManagement.Tests;

public sealed class BookingTests(FlightHost host) : IClassFixture<FlightHost>
{
    [Fact]
    public async Task LastSeatConcurrent_PostgresLockAllowsExactlyOneBooking()
    {
        using var account = await host.RegisterAndLogin();
        var passenger = await host.Passenger(account);
        var flight = await host.Flight(3);
        foreach (var seat in new[] { "1A", "1B" })
            Assert.Equal(HttpStatusCode.Created, (await account.Api.PostAsJsonAsync("/api/Tickets",
                FlightHost.Booking(flight, passenger, seat))).StatusCode);
        await CompeteBehindRowLock(flight.Id,
            () => account.Api.PostAsJsonAsync("/api/Tickets", FlightHost.Booking(flight, passenger, "2A")),
            () => account.Api.PostAsJsonAsync("/api/Tickets", FlightHost.Booking(flight, passenger, "2B")));
        await using var db = host.Db();
        Assert.Equal(3, await db.Tickets.CountAsync(t => t.FlightId == flight.Id));
    }

    [Fact]
    public async Task SameSeatConcurrent_CaseAndWhitespaceCannotBypassUniqueness()
    {
        using var account = await host.RegisterAndLogin();
        var passenger = await host.Passenger(account);
        var flight = await host.Flight(3);
        await CompeteBehindRowLock(flight.Id,
            () => account.Api.PostAsJsonAsync("/api/Tickets", FlightHost.Booking(flight, passenger, "3C")),
            () => account.Api.PostAsJsonAsync("/api/Tickets", FlightHost.Booking(flight, passenger, " 3c ")));
        await using var db = host.Db();
        var ticket = await db.Tickets.SingleAsync(t => t.FlightId == flight.Id);
        Assert.Equal("3C", ticket.NormalizedSeatNumber);
    }

    [Theory]
    [InlineData("", 1000)]
    [InlineData("123456789", 1000)]
    [InlineData("1-A", 1000)]
    [InlineData("1A", 0)]
    [InlineData("1A", -1)]
    [InlineData("1A", 0.001)]
    public async Task InvalidBooking_RejectsSeatOrPriceWithoutWriting(string seat, double price)
    {
        using var account = await host.RegisterAndLogin();
        var passenger = await host.Passenger(account);
        var flight = await host.Flight();
        var response = await account.Api.PostAsJsonAsync("/api/Tickets",
            new { flightId = flight.Id, passengerId = passenger.Id, seatNumber = seat, price = (decimal)price });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = host.Db();
        Assert.False(await db.Tickets.AnyAsync(t => t.FlightId == flight.Id));
    }

    [Fact]
    public async Task OwnerUpdate_PreservesIdentityAndPurchaseDate_RejectsFullDestination()
    {
        using var account = await host.RegisterAndLogin();
        var passenger = await host.Passenger(account);
        var from = await host.Flight(3);
        var to = await host.Flight(1);
        var created = await account.Api.PostAsJsonAsync("/api/Tickets", FlightHost.Booking(from, passenger, "1A"));
        var ticket = (await created.Content.ReadFromJsonAsync<Ticket>())!;
        // Compare the persisted timestamp: PostgreSQL stores microseconds, .NET ticks are 100ns.
        ticket = (await account.Api.GetFromJsonAsync<Ticket>($"/api/Tickets/{ticket.Id}"))!;
        Assert.Equal(HttpStatusCode.Created,
            (await account.Api.PostAsJsonAsync("/api/Tickets", FlightHost.Booking(to, passenger, "1B"))).StatusCode);
        var move = new { ticket.Id, FlightId = to.Id, PassengerId = passenger.Id, SeatNumber = "3C", Price = 1200m,
            OwnerUserId = "forged", PurchaseDate = DateTime.UtcNow.AddYears(-1), Status = "Cancelled" };
        Assert.Equal(HttpStatusCode.Conflict, (await account.Api.PutAsJsonAsync($"/api/Tickets/{ticket.Id}", move)).StatusCode);
        var edit = new { ticket.Id, FlightId = from.Id, PassengerId = passenger.Id, SeatNumber = " 2b ", Price = 1200m,
            OwnerUserId = "forged", PurchaseDate = DateTime.UtcNow.AddYears(-1), Status = "Cancelled" };
        Assert.Equal(HttpStatusCode.NoContent, (await account.Api.PutAsJsonAsync($"/api/Tickets/{ticket.Id}", edit)).StatusCode);
        await using var db = host.Db();
        var stored = (await db.Tickets.FindAsync(ticket.Id))!;
        Assert.Equal(account.Id, stored.OwnerUserId);
        Assert.Equal(ticket.PurchaseDate, stored.PurchaseDate);
        Assert.Equal("Confirmed", stored.Status);
        Assert.Equal(from.Id, stored.FlightId);
        Assert.Equal("2B", stored.SeatNumber);
    }

    [Fact]
    public async Task Cancellation_ReleasesCapacityAndSeat()
    {
        using var account = await host.RegisterAndLogin();
        var passenger = await host.Passenger(account);
        var flight = await host.Flight(1);
        var response = await account.Api.PostAsJsonAsync("/api/Tickets", FlightHost.Booking(flight, passenger));
        var ticket = (await response.Content.ReadFromJsonAsync<Ticket>())!;
        Assert.Equal(HttpStatusCode.Conflict,
            (await account.Api.PostAsJsonAsync("/api/Tickets", FlightHost.Booking(flight, passenger, "2B"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await account.Api.DeleteAsync($"/api/Tickets/{ticket.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await account.Api.PostAsJsonAsync("/api/Tickets", FlightHost.Booking(flight, passenger))).StatusCode);
    }

    [Fact]
    public async Task CapacityUpdate_RejectsOverbookedReduction_AndDeletionWithTickets()
    {
        using var admin = await host.RegisterAndLogin();
        await host.MakeAdmin(admin);
        var passenger = await host.Passenger(admin);
        var flight = await host.Flight(3);
        foreach (var seat in new[] { "1A", "1B" })
            Assert.Equal(HttpStatusCode.Created, (await admin.Api.PostAsJsonAsync("/api/Tickets",
                FlightHost.Booking(flight, passenger, seat))).StatusCode);
        flight.TotalSeats = 1;
        Assert.Equal(HttpStatusCode.Conflict, (await admin.Api.PutAsJsonAsync($"/api/Flights/{flight.Id}", flight)).StatusCode);
        flight.TotalSeats = 0;
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Api.PutAsJsonAsync($"/api/Flights/{flight.Id}", flight)).StatusCode);
        flight.TotalSeats = 2;
        Assert.Equal(HttpStatusCode.NoContent, (await admin.Api.PutAsJsonAsync($"/api/Flights/{flight.Id}", flight)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.Api.DeleteAsync($"/api/Flights/{flight.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.Api.DeleteAsync($"/api/Passengers/{passenger.Id}")).StatusCode);
        await using var db = host.Db();
        Assert.Equal(2, (await db.Flights.FindAsync(flight.Id))!.TotalSeats);
        Assert.Equal(2, await db.Tickets.CountAsync(t => t.FlightId == flight.Id));
    }

    [Fact]
    public async Task DatabaseConstraints_RejectDirectDuplicateNormalizedSeatAndInvalidPrice()
    {
        using var account = await host.RegisterAndLogin();
        var passenger = await host.Passenger(account);
        var flight = await host.Flight();
        Assert.Equal(HttpStatusCode.Created,
            (await account.Api.PostAsJsonAsync("/api/Tickets", FlightHost.Booking(flight, passenger))).StatusCode);
        await using var db = host.Db();
        db.Tickets.Add(new Ticket { FlightId = flight.Id, PassengerId = passenger.Id, SeatNumber = " 1a ",
            Price = 2000, PurchaseDate = DateTime.UtcNow });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("23505", ((PostgresException)error.InnerException!).SqlState);
        db.ChangeTracker.Clear();
        db.Tickets.Add(new Ticket { FlightId = flight.Id, PassengerId = passenger.Id, SeatNumber = "2B",
            Price = 0, PurchaseDate = DateTime.UtcNow });
        error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("23514", ((PostgresException)error.InnerException!).SqlState);
    }

    [Fact]
    public async Task CapacityReductionConcurrentWithBooking_NeverExceedsFinalCapacity()
    {
        using var admin = await host.RegisterAndLogin();
        await host.MakeAdmin(admin);
        var passenger = await host.Passenger(admin);
        var flight = await host.Flight(3);
        Assert.Equal(HttpStatusCode.Created,
            (await admin.Api.PostAsJsonAsync("/api/Tickets", FlightHost.Booking(flight, passenger))).StatusCode);
        flight.TotalSeats = 1;
        var responses = await CompeteBehindRowLock(flight.Id,
            () => admin.Api.PostAsJsonAsync("/api/Tickets", FlightHost.Booking(flight, passenger, "2B")),
            () => admin.Api.PutAsJsonAsync($"/api/Flights/{flight.Id}", flight), bookingOnly: false);
        Assert.True(
            (responses[0].StatusCode == HttpStatusCode.Created && responses[1].StatusCode == HttpStatusCode.Conflict) ||
            (responses[0].StatusCode == HttpStatusCode.Conflict && responses[1].StatusCode == HttpStatusCode.NoContent));
        await using var db = host.Db();
        var capacity = (await db.Flights.FindAsync(flight.Id))!.TotalSeats;
        var count = await db.Tickets.CountAsync(t => t.FlightId == flight.Id);
        Assert.True(count <= capacity);
        Assert.Equal(responses[0].StatusCode == HttpStatusCode.Created ? 2 : 1, count);
        Assert.Equal(responses[1].StatusCode == HttpStatusCode.NoContent ? 1 : 3, capacity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task FlightCreation_RejectsNonPositiveCapacity(int seats)
    {
        using var admin = await host.RegisterAndLogin();
        await host.MakeAdmin(admin);
        var input = new Flight { FlightNumber = "INVALID-CAPACITY", TotalSeats = seats,
            DepartureTime = DateTime.UtcNow.AddDays(1), ArrivalTime = DateTime.UtcNow.AddDays(1).AddHours(1) };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Api.PostAsJsonAsync("/api/Flights", input)).StatusCode);
        await using var db = host.Db();
        Assert.False(await db.Flights.AnyAsync(f => f.FlightNumber == "INVALID-CAPACITY"));
    }

    [Fact]
    public async Task BookingTransfer_ReservesDestinationAndReleasesSourceCapacity()
    {
        using var owner = await host.RegisterAndLogin();
        var passenger = await host.Passenger(owner);
        var from = await host.Flight(1);
        var to = await host.Flight(1);
        var response = await owner.Api.PostAsJsonAsync("/api/Tickets", FlightHost.Booking(from, passenger));
        var ticket = (await response.Content.ReadFromJsonAsync<Ticket>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Api.PutAsJsonAsync($"/api/Tickets/{ticket.Id}",
            new { ticket.Id, FlightId = to.Id, ticket.PassengerId, SeatNumber = "2B", Price = 1000m })).StatusCode);
        await using var db = host.Db();
        Assert.Equal(0, await db.Tickets.CountAsync(t => t.FlightId == from.Id));
        Assert.Equal(1, await db.Tickets.CountAsync(t => t.FlightId == to.Id));
        Assert.Equal(owner.Id, (await db.Tickets.FindAsync(ticket.Id))!.OwnerUserId);
        Assert.Equal(HttpStatusCode.Created,
            (await owner.Api.PostAsJsonAsync("/api/Tickets", FlightHost.Booking(from, passenger))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.Api.PostAsJsonAsync("/api/Tickets", FlightHost.Booking(to, passenger, "3C"))).StatusCode);
    }

    // An external lock holds both HTTP requests at the same contention point.
    // pg_stat_activity proves that the requests reached PostgreSQL; no timing race
    // or in-process semaphore can make this test pass accidentally.
    private async Task<HttpResponseMessage[]> CompeteBehindRowLock(int flightId,
        Func<Task<HttpResponseMessage>> first, Func<Task<HttpResponseMessage>> second, bool bookingOnly = true)
    {
        await using var blocker = new NpgsqlConnection(host.Connection);
        await blocker.OpenAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand("SELECT \"Id\" FROM \"Flights\" WHERE \"Id\" = @id FOR UPDATE", blocker, transaction))
        {
            command.Parameters.AddWithValue("id", flightId);
            await command.ExecuteScalarAsync();
        }
        var a = first(); var b = second();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var observer = new NpgsqlConnection(host.Connection);
        await observer.OpenAsync(timeout.Token);
        while (true)
        {
            await using var command = new NpgsqlCommand(
                "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND pid <> pg_backend_pid()", observer);
            if ((long)(await command.ExecuteScalarAsync(timeout.Token))! >= 2) break;
            await Task.Delay(25, timeout.Token);
        }
        await transaction.CommitAsync();
        var responses = await Task.WhenAll(a, b);
        if (bookingOnly)
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        }
        return responses;
    }
}
