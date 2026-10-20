using System.Net;
using System.Net.Http.Json;
using FlightManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FlightManagement.Tests;

public sealed class OwnershipTests(FlightHost host) : IClassFixture<FlightHost>
{
    [Fact]
    public async Task TicketOwnership_ServerAssignsOwner_AndDeniesForeignReadUpdateDelete()
    {
        using var alice = await host.RegisterAndLogin();
        using var bob = await host.RegisterAndLogin();
        var flight = await host.Flight();
        var passenger = await host.Passenger(alice);
        var created = await alice.Api.PostAsJsonAsync("/api/Tickets", FlightHost.Booking(flight, passenger));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var ticket = (await created.Content.ReadFromJsonAsync<Ticket>())!;
        Assert.Equal(HttpStatusCode.OK, (await alice.Api.GetAsync($"/api/Tickets/{ticket.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.Api.GetAsync($"/api/Tickets/{ticket.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.Api.PutAsJsonAsync($"/api/Tickets/{ticket.Id}", ticket)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.Api.DeleteAsync($"/api/Tickets/{ticket.Id}")).StatusCode);
        Assert.Empty((await bob.Api.GetFromJsonAsync<List<Ticket>>("/api/Tickets"))!);
        await using var db = host.Db();
        Assert.Equal(alice.Id, (await db.Tickets.FindAsync(ticket.Id))!.OwnerUserId);
        Assert.Equal(alice.Id, (await db.Passengers.FindAsync(passenger.Id))!.OwnerUserId);
        Assert.Equal("Confirmed", (await db.Tickets.FindAsync(ticket.Id))!.Status);
    }

    [Fact]
    public async Task PassengerOwnership_DeniesForeignAccessAndBooking()
    {
        using var alice = await host.RegisterAndLogin();
        using var bob = await host.RegisterAndLogin();
        var passenger = await host.Passenger(alice);
        var flight = await host.Flight();
        Assert.Empty((await bob.Api.GetFromJsonAsync<List<Passenger>>("/api/Passengers"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.Api.GetAsync($"/api/Passengers/{passenger.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.Api.PutAsJsonAsync($"/api/Passengers/{passenger.Id}", passenger)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.Api.DeleteAsync($"/api/Passengers/{passenger.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await bob.Api.PostAsJsonAsync("/api/Tickets", FlightHost.Booking(flight, passenger))).StatusCode);
    }

    [Fact]
    public async Task LegacyTickets_AreNotClaimedByMatchingNameOrEmail_AndWebHasNoFallback()
    {
        using var account = await host.RegisterAndLogin();
        var flight = await host.Flight();
        await using var db = host.Db();
        var passenger = new Passenger { FirstName = account.Username, Email = account.Username, PassportNumber = "TEST-ONLY-LEGACY-SECRET",
            DateOfBirth = DateTime.UtcNow.AddYears(-20) };
        db.Passengers.Add(passenger);
        await db.SaveChangesAsync();
        var legacy = new Ticket { FlightId = flight.Id, PassengerId = passenger.Id, SeatNumber = "1A",
            Price = 1000, PurchaseDate = DateTime.UtcNow };
        db.Tickets.Add(legacy);
        await db.SaveChangesAsync();
        Assert.Empty((await account.Api.GetFromJsonAsync<List<Ticket>>("/api/Tickets"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await account.Api.GetAsync($"/api/Tickets/{legacy.Id}")).StatusCode);
        var html = await account.Browser.GetStringAsync("/");
        Assert.DoesNotContain("TEST-ONLY-LEGACY-SECRET", html);
        Assert.Null((await db.Tickets.FindAsync(legacy.Id))!.OwnerUserId);
        Assert.Contains("У вас пока нет билетов", System.Net.WebUtility.HtmlDecode(html));
    }

    [Fact]
    public async Task WebForwarding_BookingAndCancelUseCurrentUsersCookieAndFormCsrf()
    {
        using var account = await host.RegisterAndLogin();
        var flight = await host.Flight();
        var response = await FlightHost.PostForm(account.Browser, "/?handler=Book", new()
        {
            ["BookForm.FlightId"] = flight.Id.ToString(), ["BookForm.FirstName"] = "Synthetic",
            ["BookForm.LastName"] = "Test", ["BookForm.PassportNumber"] = "TEST-ONLY",
            ["BookForm.Email"] = "synthetic@example.invalid", ["BookForm.Phone"] = "TEST",
            ["BookForm.DateOfBirth"] = "2000-01-01", ["BookForm.SeatNumber"] = "2B", ["BookForm.Price"] = "1000"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = host.Db();
        var ticket = await db.Tickets.SingleAsync(t => t.FlightId == flight.Id);
        Assert.Equal(account.Id, ticket.OwnerUserId);
        response = await FlightHost.PostForm(account.Browser, "/?handler=CancelTicket",
            new() { ["ticketId"] = ticket.Id.ToString() });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(await db.Tickets.AnyAsync(t => t.Id == ticket.Id));
    }
}
