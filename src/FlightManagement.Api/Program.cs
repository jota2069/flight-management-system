using FlightManagement.Api.Data;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using StackExchange.Redis;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING")
                          ?? "Host=localhost;Port=5432;Database=flightdb;Username=postgres;Password=postgres";

string redisConnection = Environment.GetEnvironmentVariable("REDIS_CONNECTION")
                         ?? "localhost:6379,abortConnect=false";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null)));

builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnection));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

WebApplication app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
    DbSeeder.Seed(dbContext);
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpMetrics();
app.MapMetrics();

app.UseAuthorization();
app.MapControllers();

app.Run();