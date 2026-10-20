using System.Net;
using System.Net.Http.Json;
using FlightManagement.Api.Models;
using FlightManagement.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlightManagement.Tests;

public sealed class AuthenticationTests(FlightHost host) : IClassFixture<FlightHost>
{
    [Fact]
    public async Task IdentityRoundTrip_HashesPassword_AndCookieWorksInBothHosts()
    {
        using var account = await host.RegisterAndLogin();
        Assert.Equal(HttpStatusCode.OK, (await account.Browser.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await account.Api.GetAsync("/api/Tickets")).StatusCode);
        using var scope = host.Api.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = (await users.FindByIdAsync(account.Id))!;
        Assert.NotEqual(account.Password, user.PasswordHash);
        Assert.True(await users.CheckPasswordAsync(user, account.Password));
        Assert.Empty(await users.GetRolesAsync(user));
        using var browser = host.NewWebClient();
        var demo = await FlightHost.PostForm(browser, "/Login", new() { ["Username"] = "admin", ["Password"] = "admin" });
        Assert.Equal(HttpStatusCode.OK, demo.StatusCode);
        Assert.False(demo.Headers.TryGetValues("Set-Cookie", out var cookies) &&
            cookies.Any(c => c.StartsWith(FlightSecurity.SessionCookie + "=")));
    }

    [Theory]
    [InlineData("/api/Tickets")]
    [InlineData("/api/Passengers")]
    [InlineData("/api/FlightAttendants")]
    public async Task AnonymousAndRoleGates_PrivateApiDeniesAnonymous(string path)
    {
        using var client = host.NewApiClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task AnonymousAndRoleGates_UserCannotMutateFlightsOrReadCrew()
    {
        using var account = await host.RegisterAndLogin();
        Assert.Equal(HttpStatusCode.Forbidden, (await account.Api.GetAsync("/api/FlightAttendants")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await account.Api.PostAsJsonAsync("/api/Flights", new { })).StatusCode);
        var denied = await account.Browser.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.StartsWith("https://flight.test/AccessDenied", denied.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("/Login")]
    [InlineData("/Register")]
    public async Task CsrfRequired_AccountFormsRejectMissingToken(string page)
    {
        using var browser = host.NewWebClient();
        var response = await FlightHost.PostForm(browser, page, new() { ["Username"] = "x", ["Password"] = "x" }, csrf: false);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CsrfRequired_ApiRejectsMissingInvalidAndOtherUsersToken()
    {
        using var alice = await host.RegisterAndLogin();
        using var bob = await host.RegisterAndLogin();
        var flight = await host.Flight();
        var passenger = await host.Passenger(alice);
        alice.Api.DefaultRequestHeaders.Remove(FlightSecurity.CsrfHeader);
        var body = FlightHost.Booking(flight, passenger);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.Api.PostAsJsonAsync("/api/Tickets", body)).StatusCode);
        alice.Api.DefaultRequestHeaders.Add(FlightSecurity.CsrfHeader, "not-a-token");
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.Api.PostAsJsonAsync("/api/Tickets", body)).StatusCode);
        alice.Api.DefaultRequestHeaders.Remove(FlightSecurity.CsrfHeader);
        alice.Api.DefaultRequestHeaders.Add(FlightSecurity.CsrfHeader, bob.Token);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.Api.PostAsJsonAsync("/api/Tickets", body)).StatusCode);
        await using var db = host.Db();
        Assert.False(await db.Tickets.AnyAsync(t => t.FlightId == flight.Id));
    }

    [Fact]
    public async Task OriginGuard_RejectsCrossOriginEvenWithValidCsrf()
    {
        using var account = await host.RegisterAndLogin();
        account.Api.DefaultRequestHeaders.Add("Origin", "https://hostile.invalid");
        Assert.Equal(HttpStatusCode.Forbidden, (await account.Api.PostAsJsonAsync("/api/Passengers", new { })).StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/Tickets");
        request.Headers.Add("Origin", "https://hostile.invalid");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        var response = await account.Api.SendAsync(request);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task SecurityStamp_RevokedCookieIsRejectedByBothHosts()
    {
        using var account = await host.RegisterAndLogin();
        using var scope = host.Api.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.True((await users.UpdateSecurityStampAsync((await users.FindByIdAsync(account.Id))!)).Succeeded);
        Assert.Equal(HttpStatusCode.Unauthorized, (await account.Api.GetAsync("/api/Tickets")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await account.Browser.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task Login_LocksOutAfterFiveFailedAttempts()
    {
        using var account = await host.RegisterAndLogin();
        using var browser = host.NewWebClient();
        for (var i = 0; i < 5; i++)
            await FlightHost.PostForm(browser, "/Login", new() { ["Username"] = account.Username, ["Password"] = "wrong" });
        var response = await FlightHost.PostForm(browser, "/Login",
            new() { ["Username"] = account.Username, ["Password"] = account.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // Login page, not a successful redirect.
        using var scope = host.Api.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.True(await users.IsLockedOutAsync((await users.FindByIdAsync(account.Id))!));
    }
}
