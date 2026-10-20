using System.Security.Claims;
using System.Text.RegularExpressions;
using FlightManagement.Api.Data;
using FlightManagement.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FlightManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TicketsController(AppDbContext context) : ControllerBase
{
    private string OwnerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private bool CanAccess(Ticket ticket) => User.IsInRole("Admin") || ticket.OwnerUserId == OwnerId;
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
        if (!ValidInput(input)) return BadRequest("Некорректное место или цена.");
        await using var transaction = await context.Database.BeginTransactionAsync();
        var flights = await FlightLocks.Acquire(context, input.FlightId);
        if (!flights.TryGetValue(input.FlightId, out var flight)) return BadRequest("Рейс не найден.");
        if (!await PassengerAllowed(input.PassengerId)) return BadRequest("Пассажир не найден.");
        var seat = Normalize(input.SeatNumber);
        if (await IsFullOrOccupied(flight, seat)) return Conflict("Нет свободного места.");
        var ticket = new Ticket
        {
            FlightId = input.FlightId, PassengerId = input.PassengerId,
            SeatNumber = seat, Price = input.Price, Status = "Confirmed",
            PurchaseDate = DateTime.UtcNow, OwnerUserId = OwnerId
        };
        context.Tickets.Add(ticket);
        try
        {
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" })
        {
            return Conflict("Место уже занято.");
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23503" })
        {
            return Conflict("Данные рейса или пассажира изменились.");
        }
        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> Update(int id, Ticket input)
    {
        var original = await VisibleTickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (original is null) return NotFound();
        if (id != input.Id || !ValidInput(input)) return BadRequest();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var flights = await FlightLocks.Acquire(context, original.FlightId, input.FlightId);
        var ticket = await LockedTicket(id);
        if (ticket is null || !CanAccess(ticket)) return NotFound();
        if (ticket.FlightId != original.FlightId) return Conflict("Билет изменился. Повторите запрос.");
        if (!flights.TryGetValue(input.FlightId, out var flight) || !await PassengerAllowed(input.PassengerId))
            return BadRequest();
        var seat = Normalize(input.SeatNumber);
        if (await IsFullOrOccupied(flight, seat, id)) return Conflict("Нет свободного места.");
        ticket.FlightId = input.FlightId;
        ticket.PassengerId = input.PassengerId;
        ticket.SeatNumber = seat;
        ticket.Price = input.Price;
        // Never copy identity, ownership, status, navigation objects or purchase date.
        try
        {
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" or "23503" })
        {
            return Conflict("Место или данные пассажира изменились.");
        }
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id)
    {
        var original = await VisibleTickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (original is null) return NotFound();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await FlightLocks.Acquire(context, original.FlightId);
        var ticket = await LockedTicket(id);
        if (ticket is null || !CanAccess(ticket)) return NotFound();
        if (ticket.FlightId != original.FlightId) return Conflict("Билет изменился. Повторите запрос.");
        context.Tickets.Remove(ticket);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return NoContent();
    }

    private async Task<Ticket?> LockedTicket(int id) =>
        (await context.Tickets.FromSqlInterpolated(
            $"SELECT * FROM \"Tickets\" WHERE \"Id\" = {id} FOR UPDATE").ToListAsync()).SingleOrDefault();

    private Task<bool> PassengerAllowed(int id) => context.Passengers.AnyAsync(p =>
        p.Id == id && (p.OwnerUserId == OwnerId || User.IsInRole("Admin")));

    private async Task<bool> IsFullOrOccupied(Flight flight, string seat, int exceptId = 0) =>
        await context.Tickets.CountAsync(t => t.FlightId == flight.Id && t.Id != exceptId) >= flight.TotalSeats ||
        await context.Tickets.AnyAsync(t => t.FlightId == flight.Id && t.Id != exceptId && t.NormalizedSeatNumber == seat);

    private static string Normalize(string seat) => seat.Trim().ToUpperInvariant();

    private static bool ValidInput(Ticket input) =>
        !string.IsNullOrWhiteSpace(input.SeatNumber) &&
        Regex.IsMatch(Normalize(input.SeatNumber), "^[A-Z0-9]{1,8}$", RegexOptions.CultureInvariant) &&
        input.Price > 0 && input.Price <= 9999999999999999.99m && decimal.Round(input.Price, 2) == input.Price;
}
