using FlightManagement.Api.Data;
using FlightManagement.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace FlightManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PassengersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IDatabase _redis;

    public PassengersController(AppDbContext context, IConnectionMultiplexer redis)
    {
        _context = context;
        _redis = redis.GetDatabase();
    }

    [HttpGet]
    public async Task<ActionResult<List<Passenger>>> GetAll()
    {
        string cacheKey = "passengers:all";
        string? cached = await _redis.StringGetAsync(cacheKey);

        if (cached is not null)
        {
            List<Passenger>? cachedPassengers = JsonSerializer.Deserialize<List<Passenger>>(cached);
            return Ok(cachedPassengers);
        }

        List<Passenger> passengers = await _context.Passengers
            .Include(p => p.Tickets)
            .ToListAsync();

        await _redis.StringSetAsync(cacheKey, JsonSerializer.Serialize(passengers), TimeSpan.FromMinutes(5));

        return Ok(passengers);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Passenger>> GetById(int id)
    {
        string cacheKey = $"passenger:{id}";
        string? cached = await _redis.StringGetAsync(cacheKey);

        if (cached is not null)
        {
            Passenger? cachedPassenger = JsonSerializer.Deserialize<Passenger>(cached);
            return Ok(cachedPassenger);
        }

        Passenger? passenger = await _context.Passengers
            .Include(p => p.Tickets)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (passenger is null)
        {
            return NotFound();
        }

        await _redis.StringSetAsync(cacheKey, JsonSerializer.Serialize(passenger), TimeSpan.FromMinutes(5));

        return Ok(passenger);
    }

    [HttpPost]
    public async Task<ActionResult<Passenger>> Create(Passenger passenger)
    {
        _context.Passengers.Add(passenger);
        await _context.SaveChangesAsync();

        await InvalidateCache();

        return CreatedAtAction(nameof(GetById), new { id = passenger.Id }, passenger);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> Update(int id, Passenger passenger)
    {
        if (id != passenger.Id)
        {
            return BadRequest();
        }

        _context.Entry(passenger).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _context.Passengers.AnyAsync(p => p.Id == id))
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
        Passenger? passenger = await _context.Passengers.FindAsync(id);

        if (passenger is null)
        {
            return NotFound();
        }

        _context.Passengers.Remove(passenger);
        await _context.SaveChangesAsync();

        await InvalidateCache(id);

        return NoContent();
    }

    private async Task InvalidateCache(int? id = null)
    {
        await _redis.KeyDeleteAsync("passengers:all");

        if (id.HasValue)
        {
            await _redis.KeyDeleteAsync($"passenger:{id}");
        }
    }
}