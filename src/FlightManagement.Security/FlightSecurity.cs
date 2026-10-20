using System.Net;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FlightManagement.Security;

public static class FlightSecurity
{
    public const string SessionCookie = "Flight.Session";
    public const string CsrfCookie = "Flight.Csrf";
    public const string CsrfHeader = "X-CSRF-TOKEN";

    public static string ConnectionString(IConfiguration config) =>
        config["CONNECTION_STRING"] ?? throw new InvalidOperationException("CONNECTION_STRING is required.");

    public static void AddFlightIdentity<TContext>(this IServiceCollection services,
        IConfiguration config, IHostEnvironment environment) where TContext : FlightIdentityDbContext
    {
        var keyPath = config["KEY_RING_PATH"]
            ?? throw new InvalidOperationException("KEY_RING_PATH is required; share it between Web and API.");
        if (!Path.IsPathFullyQualified(keyPath))
            throw new InvalidOperationException("KEY_RING_PATH must be absolute.");
        _ = PublicOrigin(config, environment);
        services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keyPath))
            .SetApplicationName("FlightManagement.Identity.v1");
        services.AddIdentity<IdentityUser, IdentityRole>(options =>
        {
            options.Password.RequiredLength = 12;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.User.RequireUniqueEmail = false; // Email is not an authentication/ownership key.
        }).AddEntityFrameworkStores<TContext>().AddDefaultTokenProviders();
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = SessionCookie;
            options.Cookie.Path = "/";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.LoginPath = "/Login";
            options.AccessDeniedPath = "/AccessDenied";
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.SlidingExpiration = false;
            // API clients must receive status codes, not a successful HTML login page.
            options.Events.OnRedirectToLogin = c => ApiOrRedirect(c, StatusCodes.Status401Unauthorized);
            options.Events.OnRedirectToAccessDenied = c => ApiOrRedirect(c, StatusCodes.Status403Forbidden);
        });
        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = CsrfCookie;
            options.Cookie.Path = "/";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.HeaderName = CsrfHeader;
        });
        services.AddAuthorization();
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
            // Never trust arbitrary X-Forwarded-* headers.
            foreach (var ip in config.GetSection("TrustedProxies").Get<string[]>() ?? [])
                options.KnownProxies.Add(IPAddress.Parse(ip));
        });
    }

    private static Task ApiOrRedirect(RedirectContext<CookieAuthenticationOptions> context, int status)
    {
        if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = status;
        else context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }

    private static Uri PublicOrigin(IConfiguration config, IHostEnvironment environment)
    {
        if (!Uri.TryCreate(config["PUBLIC_ORIGIN"], UriKind.Absolute, out var origin) ||
            (origin.Scheme != "https" && !(environment.IsDevelopment() && origin.Scheme == "http")) ||
            origin.AbsolutePath != "/" || origin.Query.Length != 0 ||
            origin.Fragment.Length != 0 || origin.UserInfo.Length != 0)
            throw new InvalidOperationException("PUBLIC_ORIGIN must be an HTTPS origin (HTTP only in Development).");
        return origin;
    }

    public static void UseFlightOriginGuard(this WebApplication app)
    {
        var origin = PublicOrigin(app.Configuration, app.Environment).GetLeftPart(UriPartial.Authority);
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!HttpMethods.IsGet(context.Request.Method) &&
                !HttpMethods.IsHead(context.Request.Method) &&
                !HttpMethods.IsOptions(context.Request.Method) &&
                context.Request.Headers.TryGetValue("Origin", out var supplied) &&
                !string.Equals(supplied.ToString(), origin, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            await next();
        });
    }
}
