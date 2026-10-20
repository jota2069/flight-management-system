using FlightManagement.Api.Data;
using FlightManagement.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace FlightManagement.Tests;

public sealed class SecurityConfigurationTests
{
    [Fact]
    public void Production_RequiresHttpsOrigin_AndSecureHostOnlyCookies()
    {
        string path = Path.Combine(Path.GetTempPath(), "flight-config-" + Guid.NewGuid().ToString("N"));
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);
        IConfiguration Config(string origin) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["KEY_RING_PATH"] = path, ["PUBLIC_ORIGIN"] = origin }).Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection()
            .AddFlightIdentity<AppDbContext>(Config("http://flight.test"), env.Object));
        try
        {
            var services = new ServiceCollection().AddLogging();
            services.AddFlightIdentity<AppDbContext>(Config("https://flight.test"), env.Object);
            using var provider = services.BuildServiceProvider();
            var cookie = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
            Assert.Equal(CookieSecurePolicy.Always, cookie.Cookie.SecurePolicy);
            Assert.Equal(SameSiteMode.Strict, cookie.Cookie.SameSite);
            Assert.True(cookie.Cookie.HttpOnly);
            Assert.Null(cookie.Cookie.Domain);
            Assert.Equal("/", cookie.Cookie.Path);
            var csrf = provider.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;
            Assert.Equal(CookieSecurePolicy.Always, csrf.Cookie.SecurePolicy);
            Assert.Equal(SameSiteMode.Strict, csrf.Cookie.SameSite);
            if (!OperatingSystem.IsWindows())
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(path));
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
    }
}
