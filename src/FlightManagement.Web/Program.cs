using FlightManagement.Security;
using FlightManagement.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<FlightIdentityDbContext>(options =>
    options.UseNpgsql(FlightSecurity.ConnectionString(builder.Configuration)));
builder.Services.AddFlightIdentity<FlightIdentityDbContext>(builder.Configuration, builder.Environment);
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<ApiSessionHandler>();
var apiBaseUrl = builder.Configuration["API_BASE_URL"] ??
    (builder.Environment.IsDevelopment() ? "http://localhost:8080/" :
        throw new InvalidOperationException("API_BASE_URL with HTTPS is required in production."));
if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiUri) ||
    (apiUri.Scheme != "https" && !(builder.Environment.IsDevelopment() && apiUri.Scheme == "http")) ||
    apiUri.UserInfo.Length != 0 || apiUri.AbsolutePath != "/" ||
    apiUri.Query.Length != 0 || apiUri.Fragment.Length != 0)
    throw new InvalidOperationException("API_BASE_URL must be an HTTPS origin (HTTP only in Development).");
builder.Services.AddHttpClient<FlightApiService>(client =>
{
    client.BaseAddress = apiUri;
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    UseCookies = false,
    AllowAutoRedirect = false
}).AddHttpMessageHandler<ApiSessionHandler>();
builder.Services.AddRazorPages();
var app = builder.Build();
if (app.Configuration.GetSection("TrustedProxies").GetChildren().Any()) app.UseForwardedHeaders();
app.UseFlightOriginGuard();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
if (args is ["--create-admin", var username])
{
    using var scope = app.Services.CreateScope();
    Console.Write("New administrator password (not echoed): ");
    var result = await AdminProvisioning.Create(
        scope.ServiceProvider.GetRequiredService<FlightIdentityDbContext>(),
        scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Microsoft.AspNetCore.Identity.IdentityUser>>(),
        scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole>>(),
        username, AdminProvisioning.ReadPassword());
    Console.WriteLine(result.Succeeded ? "Administrator created." : string.Join("; ", result.Errors.Select(e => e.Description)));
    Environment.ExitCode = result.Succeeded ? 0 : 1;
    return;
}
app.Run();

public partial class WebProgram { }
