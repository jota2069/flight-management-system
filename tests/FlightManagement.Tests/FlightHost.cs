using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FlightManagement.Api.Data;
using FlightManagement.Api.Models;
using FlightManagement.Security;
using FlightManagement.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Npgsql;
using StackExchange.Redis;

namespace FlightManagement.Tests;

public sealed class FlightHost : IAsyncLifetime
{
    private string _adminConnection = "";
    public string EnvironmentName { get; init; } = "Development";
    public string Connection { get; private set; } = "";
    public string KeyPath { get; } = Path.Combine(Path.GetTempPath(), "flight-test-keys-" + Guid.NewGuid().ToString("N"));
    public string Database { get; } = "flight_test_" + Guid.NewGuid().ToString("N");
    public WebApplicationFactory<ApiProgram> Api { get; private set; } = null!;
    public WebApplicationFactory<WebProgram> Web { get; private set; } = null!;
    public Mock<IDatabase> Redis { get; } = new();
    public Mock<IConnectionMultiplexer> Multiplexer { get; } = new();

    public async Task InitializeAsync()
    {
        _adminConnection = Environment.GetEnvironmentVariable("FLIGHT_TEST_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set FLIGHT_TEST_ADMIN_CONNECTION to an isolated PostgreSQL instance with CREATEDB. Tests never use the application database.");
        await using (var db = new NpgsqlConnection(_adminConnection))
        {
            await db.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{Database}\"", db);
            await command.ExecuteNonQueryAsync();
        }
        Connection = new NpgsqlConnectionStringBuilder(_adminConnection) { Database = Database }.ConnectionString;
        Multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(Redis.Object);
        Redis.Setup(r => r.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);
        Api = new ApiFactory(this);
        _ = Api.Server; // Runs real migrations before the Web Identity store is used.
        Web = new WebFactory(this);
        _ = Web.Server;
    }

    public HttpClient NewWebClient() => Web.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://flight.test"), AllowAutoRedirect = false
    });

    public HttpClient NewApiClient() => Api.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://flight.test"), AllowAutoRedirect = false
    });

    public async Task<Account> RegisterAndLogin(string? username = null)
    {
        username ??= "u" + Guid.NewGuid().ToString("N");
        var password = "Aa1!" + Guid.NewGuid().ToString("N");
        var browser = NewWebClient();
        var registration = await PostForm(browser, "/Register", new() { ["Username"] = username, ["Password"] = password });
        Assert.Equal(HttpStatusCode.Redirect, registration.StatusCode);
        var login = await PostForm(browser, "/Login", new() { ["Username"] = username, ["Password"] = password });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var session = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(FlightSecurity.SessionCookie + "=")).Split(';')[0];
        var api = NewApiClient();
        api.DefaultRequestHeaders.Add("Cookie", session);
        var tokens = await api.GetFromJsonAsync<CsrfResponse>("/api/security/csrf");
        api.DefaultRequestHeaders.Add(FlightSecurity.CsrfHeader, tokens!.RequestToken);
        using var scope = Api.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByNameAsync(username);
        return new Account(user!.Id, username, password, browser, api, session, tokens.RequestToken);
    }

    public static async Task<HttpResponseMessage> PostForm(HttpClient client, string url,
        Dictionary<string, string> fields, bool csrf = true)
    {
        if (csrf) fields["__RequestVerificationToken"] = await FormToken(client, url.Split('?')[0]);
        return await client.PostAsync(url, new FormUrlEncodedContent(fields));
    }

    public static async Task<string> FormToken(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Razor form must contain a real antiforgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    public AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Connection).Options);

    public async Task<Flight> Flight(int seats = 3)
    {
        await using var db = Db();
        var flight = new Flight { FlightNumber = "TEST-" + Guid.NewGuid().ToString("N"),
            Origin = "Test origin", Destination = "Test destination", TotalSeats = seats,
            DepartureTime = DateTime.UtcNow.AddDays(5), ArrivalTime = DateTime.UtcNow.AddDays(5).AddHours(2) };
        db.Flights.Add(flight);
        await db.SaveChangesAsync();
        return flight;
    }

    public async Task<Passenger> Passenger(Account account)
    {
        var response = await account.Api.PostAsJsonAsync("/api/Passengers", new {
            FirstName = "Synthetic", LastName = "Test", PassportNumber = "TEST-ONLY",
            Email = "synthetic@example.invalid", Phone = "", DateOfBirth = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            OwnerUserId = "client-cannot-choose-owner"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Passenger>())!;
    }

    public static object Booking(Flight flight, Passenger passenger, string seat = "1A") =>
        new { FlightId = flight.Id, PassengerId = passenger.Id, SeatNumber = seat, Price = 1000m,
            OwnerUserId = "client-cannot-choose-owner", Status = "Cancelled" };

    public async Task MakeAdmin(Account account)
    {
        using var scope = Api.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync("Admin")) Assert.True((await roles.CreateAsync(new IdentityRole("Admin"))).Succeeded);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = (await users.FindByIdAsync(account.Id))!;
        Assert.True((await users.AddToRoleAsync(user, "Admin")).Succeeded);
        // Existing cookie intentionally cannot gain a new role without signing in again.
    }

    public async Task DisposeAsync()
    {
        Web?.Dispose(); Api?.Dispose();
        NpgsqlConnection.ClearAllPools();
        await using var db = new NpgsqlConnection(_adminConnection);
        await db.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{Database}\" WITH (FORCE)", db);
        await command.ExecuteNonQueryAsync();
        if (Directory.Exists(KeyPath)) Directory.Delete(KeyPath, recursive: true);
    }

    private static void Configure(IWebHostBuilder builder, FlightHost host)
    {
        builder.UseEnvironment(host.EnvironmentName).UseSetting("CONNECTION_STRING", host.Connection)
            .UseSetting("KEY_RING_PATH", host.KeyPath).UseSetting("PUBLIC_ORIGIN", "https://flight.test")
            .UseSetting("API_BASE_URL", "https://flight.test/")
            .UseSetting("Logging:LogLevel:Default", "Warning");
    }

    private sealed class ApiFactory(FlightHost host) : WebApplicationFactory<ApiProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Configure(builder, host);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<Lazy<IConnectionMultiplexer>>();
                services.AddSingleton(new Lazy<IConnectionMultiplexer>(() => host.Multiplexer.Object));
            });
        }
    }

    private sealed class WebFactory(FlightHost host) : WebApplicationFactory<WebProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Configure(builder, host);
            builder.ConfigureServices(services => services.AddHttpClient<FlightApiService>()
                .ConfigurePrimaryHttpMessageHandler(() => host.Api.Server.CreateHandler()));
        }
    }

    private sealed record CsrfResponse(string RequestToken);
    public sealed record Account(string Id, string Username, string Password, HttpClient Browser,
        HttpClient Api, string Session, string Token) : IDisposable
    {
        public void Dispose() { Browser.Dispose(); Api.Dispose(); }
    }
}
