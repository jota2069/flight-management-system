using Microsoft.AspNetCore.Authorization;
using FlightManagement.Api.Data;
using FlightManagement.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FlightManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FlightsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IDatabase _redis;

    public FlightsController(AppDbContext context, IConnectionMultiplexer redis)
    {
        _context = context;
        _redis = redis.GetDatabase();
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<Flight>>> GetAll()
    {
        string cacheKey = "schedule:v2:flights:all";
        string? cached = await _redis.StringGetAsync(cacheKey);

        // Добавляем настройки для игнорирования циклических ссылок
        JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            PropertyNameCaseInsensitive = true
        };

        if (cached is not null)
        {
            List<Flight>? cachedFlights = JsonSerializer.Deserialize<List<Flight>>(cached, jsonOptions);
            return Ok(cachedFlights);
        }

        List<Flight> flights = await _context.Flights
            .ToListAsync();

        string serializedFlights = JsonSerializer.Serialize(flights, jsonOptions);
        await _redis.StringSetAsync(cacheKey, serializedFlights, TimeSpan.FromMinutes(5));

        return Ok(flights);
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<Flight>> GetById(int id)
    {
        string cacheKey = $"schedule:v2:flight:{id}";
        string? cached = await _redis.StringGetAsync(cacheKey);

        JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            PropertyNameCaseInsensitive = true
        };

        if (cached is not null)
        {
            Flight? cachedFlight = JsonSerializer.Deserialize<Flight>(cached, jsonOptions);
            return Ok(cachedFlight);
        }

        Flight? flight = await _context.Flights
            .Include(f => f.Tickets)
            .Include(f => f.CrewAssignments)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (flight is null)
        {
            return NotFound();
        }

        string serializedFlight = JsonSerializer.Serialize(flight, jsonOptions);
        await _redis.StringSetAsync(cacheKey, serializedFlight, TimeSpan.FromMinutes(5));

        return Ok(flight);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Flight>> Create(Flight input)
    {
        if (!ValidFlight(input)) return BadRequest("Некорректные параметры рейса.");
        var flight = new Flight();
        Copy(input, flight);
        _context.Flights.Add(flight);
        await _context.SaveChangesAsync();
        await InvalidateCache();
        return CreatedAtAction(nameof(GetById), new { id = flight.Id }, flight);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Update(int id, Flight input)
    {
        if (id != input.Id || !ValidFlight(input)) return BadRequest();
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var flights = await FlightLocks.Acquire(_context, id);
        if (!flights.TryGetValue(id, out var flight)) return NotFound();
        if (await _context.Tickets.CountAsync(t => t.FlightId == id) > input.TotalSeats)
            return Conflict("Вместимость меньше числа забронированных мест.");
        Copy(input, flight);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        await InvalidateCache(id);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Delete(int id)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var flights = await FlightLocks.Acquire(_context, id);
        if (!flights.TryGetValue(id, out var flight)) return NotFound();
        if (await _context.Tickets.AnyAsync(t => t.FlightId == id))
            return Conflict("Сначала отмените билеты рейса.");
        _context.Flights.Remove(flight);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        await InvalidateCache(id);
        return NoContent();
    }

    private static bool ValidFlight(Flight input) => input.TotalSeats > 0 &&
        input.ArrivalTime > input.DepartureTime && !string.IsNullOrWhiteSpace(input.FlightNumber);

    private static void Copy(Flight input, Flight flight)
    {
        flight.FlightNumber = input.FlightNumber;
        flight.Origin = input.Origin;
        flight.Destination = input.Destination;
        flight.DepartureTime = DateTime.SpecifyKind(input.DepartureTime, DateTimeKind.Utc);
        flight.ArrivalTime = DateTime.SpecifyKind(input.ArrivalTime, DateTimeKind.Utc);
        flight.TotalSeats = input.TotalSeats;
        flight.Status = input.Status;
    }

    private async Task InvalidateCache(int? id = null)
    {
        await _redis.KeyDeleteAsync("schedule:v2:flights:all");
        if (id.HasValue) await _redis.KeyDeleteAsync($"schedule:v2:flight:{id}");
    }
}
