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
    public async Task<ActionResult<List<Flight>>> GetAll()
    {
        string cacheKey = "flights:all";
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
            .Include(f => f.Tickets)
            .Include(f => f.CrewAssignments)
            .ToListAsync();

        string serializedFlights = JsonSerializer.Serialize(flights, jsonOptions);
        await _redis.StringSetAsync(cacheKey, serializedFlights, TimeSpan.FromMinutes(5));

        return Ok(flights);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Flight>> GetById(int id)
    {
        string cacheKey = $"flight:{id}";
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
    public async Task<ActionResult<Flight>> Create(Flight flight)
    {
        _context.Flights.Add(flight);
        await _context.SaveChangesAsync();

        await InvalidateCache();

        return CreatedAtAction(nameof(GetById), new { id = flight.Id }, flight);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> Update(int id, Flight flight)
    {
        if (id != flight.Id)
        {
            return BadRequest();
        }

        _context.Entry(flight).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            bool exists = await _context.Flights.AnyAsync(f => f.Id == id);
            if (!exists)
            {
                return NotFound();
            }
            throw;
        }

        await InvalidateCache(id);

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id)
    {
        Flight? flight = await _context.Flights.FindAsync(id);

        if (flight is null)
        {
            return NotFound();
        }

        _context.Flights.Remove(flight);
        await _context.SaveChangesAsync();

        await InvalidateCache(id);

        return NoContent();
    }

    private async Task InvalidateCache(int? id = null)
    {
        await _redis.KeyDeleteAsync("flights:all");

        if (id.HasValue)
        {
            await _redis.KeyDeleteAsync($"flight:{id}");
        }
    }
}