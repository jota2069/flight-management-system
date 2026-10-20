using FlightManagement.Api.Data;
using FlightManagement.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlightManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FlightsController(AppDbContext context, ScheduleCache cache) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<Flight>>> GetAll() =>
        Ok(await cache.Read<List<Flight>>("flights:all",
            async () => await context.Flights.AsNoTracking().OrderBy(f => f.Id).ToListAsync()));

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<Flight>> GetById(int id)
    {
        var flight = await cache.Read<Flight>($"flight:{id}",
            () => context.Flights.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id));
        return flight is null ? NotFound() : Ok(flight);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Flight>> Create(Flight input)
    {
        if (!ValidFlight(input)) return BadRequest("Некорректные параметры рейса.");
        var flight = new Flight();
        Copy(input, flight);
        await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        context.Flights.Add(flight);
        await context.SaveChangesAsync();
        await ScheduleCache.Advance(context);
        await transaction.CommitAsync();
        return CreatedAtAction(nameof(GetById), new { id = flight.Id }, flight);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Update(int id, Flight input)
    {
        if (id != input.Id || !ValidFlight(input)) return BadRequest();
        await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        var flights = await FlightLocks.Acquire(context, id);
        if (!flights.TryGetValue(id, out var flight)) return NotFound();
        if (await context.Tickets.CountAsync(t => t.FlightId == id) > input.TotalSeats)
            return Conflict("Вместимость меньше числа забронированных мест.");
        Copy(input, flight);
        await context.SaveChangesAsync();
        await ScheduleCache.Advance(context);
        await transaction.CommitAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Delete(int id)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        var flights = await FlightLocks.Acquire(context, id);
        if (!flights.TryGetValue(id, out var flight)) return NotFound();
        if (await context.Tickets.AnyAsync(t => t.FlightId == id))
            return Conflict("Сначала отмените билеты рейса.");
        context.Flights.Remove(flight);
        await context.SaveChangesAsync();
        await ScheduleCache.Advance(context);
        await transaction.CommitAsync();
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

}
