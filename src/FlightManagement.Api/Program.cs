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
builder.Services.AddSingleton(_ => new Lazy<IConnectionMultiplexer>(() =>
{
    var redis = ConfigurationOptions.Parse(builder.Configuration["REDIS_CONNECTION"] ?? "localhost:6379");
    redis.AbortOnConnectFail = false;
    redis.ConnectTimeout = 500;
    redis.ConnectRetry = 0;
    redis.AsyncTimeout = 500;
    redis.SyncTimeout = 500;
    return ConnectionMultiplexer.Connect(redis);
}));
builder.Services.AddScoped<ScheduleCache>();
// Dispose the shared connection without forcing it to connect on shutdown.
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
app.Lifetime.ApplicationStopped.Register(() =>
{
    var redis = app.Services.GetRequiredService<Lazy<IConnectionMultiplexer>>();
    if (redis.IsValueCreated) redis.Value.Dispose();
});
app.Run();

public partial class ApiProgram { }
