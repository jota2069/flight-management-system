using FlightManagement.Security;
using FlightManagement.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<FlightIdentityDbContext>(options =>
    options.UseNpgsql(FlightSecurity.ConnectionString(builder.Configuration)));
builder.Services.AddFlightIdentity<FlightIdentityDbContext>(builder.Configuration, builder.Environment);
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<ApiSessionHandler>();
builder.Services.AddHttpClient<FlightApiService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["API_BASE_URL"] ?? "http://localhost:8080/");
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
app.Run();

public partial class WebProgram { }
