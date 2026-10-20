using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace FlightManagement.Tests;

public sealed class MigrationTests(FlightHost host) : IClassFixture<FlightHost>
{
    [Fact]
    public async Task MigrationPreservesLegacy_WithoutAssigningOwnersOrCreatingDemoUsers()
    {
        await using var db = host.Db();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260327003424_InitialCreate");
        await InsertLegacy(db, 3, "1A", 1000);
        await migrator.MigrateAsync();
        var ticket = await db.Tickets.AsNoTracking().SingleAsync();
        Assert.Null(ticket.OwnerUserId);
        Assert.Equal("1A", ticket.SeatNumber);
        Assert.Equal(1000m, ticket.Price);
        Assert.Null((await db.Passengers.SingleAsync()).OwnerUserId);
        Assert.Equal(0, await db.Users.CountAsync());
        Assert.Equal("1A", ticket.NormalizedSeatNumber);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("capacity")]
    [InlineData("price")]
    [InlineData("seat")]
    public async Task MigrationRejectsInvalidLegacy_WithoutDeletingOrReassigningData(string invalid)
    {
        await using var db = host.Db();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260327003424_InitialCreate");
        await InsertLegacy(db, invalid == "capacity" ? 1 : 3,
            invalid == "seat" ? "bad-label!" : "1A", invalid == "price" ? 0 : 1000);
        if (invalid is "duplicate" or "capacity")
        {
            var seat = invalid == "duplicate" ? " 1a " : "2B";
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""INSERT INTO "Tickets" ("SeatNumber","Price","Status","PurchaseDate","FlightId","PassengerId") VALUES ({seat},1000,'Confirmed',now(),1,1)""");
        }
        var error = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync());
        Assert.Contains("Booking migration blocked", error.MessageText);
        // Read the old-compatible columns even when the new computed column is absent.
        await using var connection = new NpgsqlConnection(host.Connection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""SELECT count(*) FROM "Tickets" """, connection);
        Assert.Equal(invalid is "duplicate" or "capacity" ? 2L : 1L, (long)(await command.ExecuteScalarAsync())!);
        Assert.Equal(0, await db.Users.CountAsync());
    }

    private static Task InsertLegacy(FlightManagement.Api.Data.AppDbContext db, int seats, string seat, decimal price) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM "Tickets"; DELETE FROM "Passengers"; DELETE FROM "Flights";
            INSERT INTO "Flights" ("Id","FlightNumber","Origin","Destination","DepartureTime","ArrivalTime","TotalSeats","Status")
              VALUES (1,'LEGACY','Test','Test',now(),now()+interval '1 hour',{seats},'Scheduled');
            INSERT INTO "Passengers" ("Id","FirstName","LastName","PassportNumber","Email","Phone","DateOfBirth")
              VALUES (1,'Synthetic','Test','TEST-ONLY','synthetic@example.invalid','TEST',now()-interval '20 years');
            INSERT INTO "Tickets" ("SeatNumber","Price","Status","PurchaseDate","FlightId","PassengerId")
              VALUES ({seat},{price},'Confirmed',now(),1,1);
            """);
}
