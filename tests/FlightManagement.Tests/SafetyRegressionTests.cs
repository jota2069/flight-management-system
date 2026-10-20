using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using FlightManagement.Api.Data;
using FlightManagement.Api.Models;
using FlightManagement.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace FlightManagement.Tests;

public sealed class SafetyRegressionTests(FlightHost host) : IClassFixture<FlightHost>
{
    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task CsrfRequired_TicketMutationsRejectMissingTokenAndPreserveRecord(string method)
    {
        using var owner = await host.RegisterAndLogin();
        var passenger = await host.Passenger(owner);
        var flight = await host.Flight();
        var created = await owner.Api.PostAsJsonAsync("/api/Tickets", FlightHost.Booking(flight, passenger));
        var ticket = (await created.Content.ReadFromJsonAsync<Ticket>())!;
        owner.Api.DefaultRequestHeaders.Remove(FlightSecurity.CsrfHeader);
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/Tickets/{ticket.Id}")
        { Content = JsonContent.Create(new { ticket.Id, ticket.FlightId, ticket.PassengerId, SeatNumber = "2B", Price = 1000m }) };
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Api.SendAsync(request)).StatusCode);
        await using var db = host.Db();
        Assert.Equal("1A", (await db.Tickets.FindAsync(ticket.Id))!.SeatNumber);
    }

    [Theory]
    [InlineData("Book")]
    [InlineData("CancelTicket")]
    public async Task CsrfRequired_WebMutationRejectsMissingFormToken(string handler)
    {
        using var account = await host.RegisterAndLogin();
        var response = await FlightHost.PostForm(account.Browser, "/?handler=" + handler,
            new() { ["ticketId"] = "1" }, csrf: false);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BrowserSessions_NeverLeakBetweenPooledHttpClients_AndCannotCancelForeignTicket()
    {
        using var alice = await host.RegisterAndLogin();
        using var bob = await host.RegisterAndLogin();
        var passenger = await host.Passenger(alice);
        var flight = await host.Flight();
        var response = await alice.Api.PostAsJsonAsync("/api/Tickets", FlightHost.Booking(flight, passenger, "ALICE8"));
        var ticket = (await response.Content.ReadFromJsonAsync<Ticket>())!;
        Assert.Contains("ALICE8", await alice.Browser.GetStringAsync("/"));
        Assert.DoesNotContain("ALICE8", await bob.Browser.GetStringAsync("/"));
        Assert.Contains("ALICE8", await alice.Browser.GetStringAsync("/"));
        Assert.Equal(HttpStatusCode.OK, (await FlightHost.PostForm(bob.Browser, "/?handler=CancelTicket",
            new() { ["ticketId"] = ticket.Id.ToString() })).StatusCode);
        await using var db = host.Db();
        Assert.Equal(alice.Id, (await db.Tickets.FindAsync(ticket.Id))!.OwnerUserId);
    }

    [Fact]
    public async Task SessionCookies_HaveSecurityFlags_AndLogoutRevokesApiCookie()
    {
        using var account = await host.RegisterAndLogin();
        var login = await FlightHost.PostForm(account.Browser, "/Login", new() {
            ["Username"] = account.Username, ["Password"] = account.Password });
        var cookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(FlightSecurity.SessionCookie + "="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, (await account.Api.GetAsync("/api/Tickets")).StatusCode);
        var logout = await FlightHost.PostForm(account.Browser, "/?handler=Logout", new());
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await account.Api.GetAsync("/api/Tickets")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await account.Browser.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task Admin_CanReviewLegacyTicket_ButCannotAssignOwnerThroughUpdate()
    {
        using var admin = await host.RegisterAndLogin();
        await host.MakeAdmin(admin);
        var flight = await host.Flight();
        var passenger = await host.Passenger(admin);
        await using var db = host.Db();
        var ticket = new Ticket { FlightId = flight.Id, PassengerId = passenger.Id, SeatNumber = "1A",
            Price = 1000, PurchaseDate = DateTime.UtcNow };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.OK, (await admin.Api.GetAsync($"/api/Tickets/{ticket.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.Api.PutAsJsonAsync($"/api/Tickets/{ticket.Id}", new {
            ticket.Id, ticket.FlightId, ticket.PassengerId, SeatNumber = "2B", Price = 1000m, OwnerUserId = admin.Id })).StatusCode);
        db.ChangeTracker.Clear();
        Assert.Null((await db.Tickets.FindAsync(ticket.Id))!.OwnerUserId);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.Api.DeleteAsync($"/api/Tickets/{ticket.Id}")).StatusCode);
        Assert.False(await db.Tickets.AnyAsync(t => t.Id == ticket.Id));
    }

    [Fact]
    public async Task AdminProvisioning_DoesNotPromoteExistingAccount()
    {
        using var user = await host.RegisterAndLogin();
        using var scope = host.Web.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var result = await AdminProvisioning.Create(
            scope.ServiceProvider.GetRequiredService<FlightIdentityDbContext>(), users,
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(), user.Username, user.Password);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == "ExistingAccount");
        Assert.False(await users.IsInRoleAsync((await users.FindByIdAsync(user.Id))!, "Admin"));
    }

    [Fact]
    public async Task AdminProvisioning_CreatesHashedAccount_AndRollsBackWeakPassword()
    {
        using var scope = host.Web.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var db = scope.ServiceProvider.GetRequiredService<FlightIdentityDbContext>();
        int usersBefore = await db.Users.CountAsync(), rolesBefore = await db.Roles.CountAsync();
        var rejected = await AdminProvisioning.Create(db, users, roles, "weak-" + Guid.NewGuid().ToString("N"), "weak");
        Assert.False(rejected.Succeeded);
        Assert.Equal(usersBefore, await db.Users.CountAsync());
        Assert.Equal(rolesBefore, await db.Roles.CountAsync());
        db.ChangeTracker.Clear();
        var name = "admin-" + Guid.NewGuid().ToString("N");
        var password = "Aa1!" + Guid.NewGuid().ToString("N");
        Assert.True((await AdminProvisioning.Create(db, users, roles, name, password)).Succeeded);
        var admin = (await users.FindByNameAsync(name))!;
        Assert.True(await users.IsInRoleAsync(admin, "Admin"));
        Assert.NotEqual(password, admin.PasswordHash);
        Assert.True(await users.CheckPasswordAsync(admin, password));
    }

    [Fact]
    public async Task CacheInitialConnectionFailure_DoesNotPreventDatabaseRead()
    {
        var flight = await host.Flight();
        await using var db = host.Db();
        var cache = new ScheduleCache(db, new Lazy<IConnectionMultiplexer>(() =>
            throw new RedisConnectionException(ConnectionFailureType.SocketFailure, "Synthetic first connection failure")),
            NullLogger<ScheduleCache>.Instance);
        var result = await cache.Read<Flight>("initial-failure",
            () => db.Flights.AsNoTracking().SingleOrDefaultAsync(f => f.Id == flight.Id));
        Assert.Equal(flight.FlightNumber, result!.FlightNumber);
    }

    [Fact]
    public async Task RealRedisConnectionRefused_PublicReadStillSucceeds()
    {
        var flight = await host.Flight();
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int closedPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        using var factory = new DisconnectedRedisFactory(host, closedPort);
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://flight.test"), AllowAutoRedirect = false });
        var result = await client.GetFromJsonAsync<Flight>($"/api/Flights/{flight.Id}");
        Assert.Equal(flight.FlightNumber, result!.FlightNumber);
        Assert.Equal(flight.TotalSeats, result.TotalSeats);
    }

    private sealed class DisconnectedRedisFactory(FlightHost host, int port) :
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<ApiProgram>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("CONNECTION_STRING", host.Connection)
                .UseSetting("KEY_RING_PATH", host.KeyPath).UseSetting("PUBLIC_ORIGIN", "https://flight.test")
                .UseSetting("REDIS_CONNECTION", $"127.0.0.1:{port}")
                .UseSetting("Logging:LogLevel:Default", "Warning");
        }
    }
}
