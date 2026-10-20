using System.Security.Claims;
using FlightManagement.Api.Data;
using FlightManagement.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlightManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TicketsController(AppDbContext context) : ControllerBase
{
    private string OwnerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private IQueryable<Ticket> VisibleTickets => User.IsInRole("Admin")
        ? context.Tickets : context.Tickets.Where(t => t.OwnerUserId == OwnerId);

    [HttpGet]
    public async Task<ActionResult<List<Ticket>>> GetAll() =>
        Ok(await VisibleTickets.AsNoTracking().Include(t => t.Flight).Include(t => t.Passenger).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Ticket>> GetById(int id)
    {
        var ticket = await VisibleTickets.AsNoTracking().Include(t => t.Flight)
            .Include(t => t.Passenger).FirstOrDefaultAsync(t => t.Id == id);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpPost]
    public async Task<ActionResult<Ticket>> Create(Ticket input)
    {
        if (!await context.Flights.AnyAsync(f => f.Id == input.FlightId) ||
            !await context.Passengers.AnyAsync(p => p.Id == input.PassengerId &&
                (p.OwnerUserId == OwnerId || User.IsInRole("Admin"))))
            return BadRequest("Рейс или доступный пассажир не найден.");
        var ticket = new Ticket
        {
            FlightId = input.FlightId, PassengerId = input.PassengerId,
            SeatNumber = input.SeatNumber, Price = input.Price,
            Status = "Confirmed", PurchaseDate = DateTime.UtcNow, OwnerUserId = OwnerId
        };
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> Update(int id, Ticket input)
    {
        var ticket = await VisibleTickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null) return NotFound();
        if (id != input.Id || !await context.Flights.AnyAsync(f => f.Id == input.FlightId) ||
            !await context.Passengers.AnyAsync(p => p.Id == input.PassengerId &&
                (p.OwnerUserId == OwnerId || User.IsInRole("Admin"))))
            return BadRequest();
        ticket.FlightId = input.FlightId;
        ticket.PassengerId = input.PassengerId;
        ticket.SeatNumber = input.SeatNumber;
        ticket.Price = input.Price;
        // OwnerUserId, purchase date and status are never assigned from client data.
        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id)
    {
        var ticket = await VisibleTickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null) return NotFound();
        context.Tickets.Remove(ticket);
        await context.SaveChangesAsync();
        return NoContent();
    }
}
