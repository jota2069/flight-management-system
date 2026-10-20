using FlightManagement.Api.Data;
using FlightManagement.Security;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(FlightSecurity.ConnectionString(builder.Configuration)));
builder.Services.AddFlightIdentity<AppDbContext>(builder.Configuration, builder.Environment);
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration["REDIS_CONNECTION"] ?? "localhost:6379,abortConnect=false"));
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add(new AuthorizeFilter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    if (builder.Configuration.GetValue<bool>("SeedDemoData")) DbSeeder.Seed(db);
}
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
if (app.Configuration.GetSection("TrustedProxies").GetChildren().Any()) app.UseForwardedHeaders();
app.UseFlightOriginGuard();
app.UseHttpMetrics();
app.MapMetrics();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class ApiProgram { }
