using FlightManagement.Web.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string apiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL")
                    ?? "http://localhost:8080/";

builder.Services.AddHttpClient<FlightApiService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/AccessDenied";
    });

builder.Services.AddAuthorization();
builder.Services.AddRazorPages();

WebApplication app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();