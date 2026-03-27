using FlightManagement.Api.Data;
using FlightManagement.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlightManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly AppDbContext _context;

    public TicketsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<Ticket>>> GetAll()
    {
        List<Ticket> tickets = await _context.Tickets
            .Include(t => t.Flight)
            .Include(t => t.Passenger)
            .ToListAsync();

        return Ok(tickets);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Ticket>> GetById(int id)
    {
        Ticket? ticket = await _context.Tickets
            .Include(t => t.Flight)
            .Include(t => t.Passenger)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket is null)
        {
            return NotFound();
        }

        return Ok(ticket);
    }

    [HttpPost]
    public async Task<ActionResult<Ticket>> Create(Ticket ticket)
    {
        bool flightExists = await _context.Flights.AnyAsync(f => f.Id == ticket.FlightId);
        bool passengerExists = await _context.Passengers.AnyAsync(p => p.Id == ticket.PassengerId);

        if (!flightExists || !passengerExists)
        {
            return BadRequest("Указанный рейс или пассажир не существует.");
        }

        ticket.PurchaseDate = DateTime.UtcNow;
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> Update(int id, Ticket ticket)
    {
        if (id != ticket.Id)
        {
            return BadRequest();
        }

        bool flightExists = await _context.Flights.AnyAsync(f => f.Id == ticket.FlightId);
        bool passengerExists = await _context.Passengers.AnyAsync(p => p.Id == ticket.PassengerId);

        if (!flightExists || !passengerExists)
        {
            return BadRequest("Указанный рейс или пассажир не существует.");
        }

        _context.Entry(ticket).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _context.Tickets.AnyAsync(t => t.Id == id))
            {
                return NotFound();
            }
            throw;
        }

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id)
    {
        Ticket? ticket = await _context.Tickets.FindAsync(id);

        if (ticket is null)
        {
            return NotFound();
        }

        _context.Tickets.Remove(ticket);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}