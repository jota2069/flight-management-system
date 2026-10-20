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
public class PassengersController(AppDbContext context) : ControllerBase
{
    private string OwnerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private IQueryable<Passenger> Visible => User.IsInRole("Admin")
        ? context.Passengers : context.Passengers.Where(p => p.OwnerUserId == OwnerId);

    [HttpGet]
    public async Task<ActionResult<List<Passenger>>> GetAll() => Ok(await Visible.AsNoTracking().ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Passenger>> GetById(int id)
    {
        var passenger = await Visible.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        return passenger is null ? NotFound() : Ok(passenger);
    }

    [HttpPost]
    public async Task<ActionResult<Passenger>> Create(Passenger input)
    {
        var passenger = new Passenger { OwnerUserId = OwnerId };
        Copy(input, passenger);
        context.Passengers.Add(passenger);
        await context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = passenger.Id }, passenger);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> Update(int id, Passenger input)
    {
        var passenger = await Visible.FirstOrDefaultAsync(p => p.Id == id);
        if (passenger is null) return NotFound();
        if (id != input.Id) return BadRequest();
        Copy(input, passenger);
        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")] // A cascade would bypass the ticket-booking transaction.
    public async Task<ActionResult> Delete(int id)
    {
        var passenger = await context.Passengers.FindAsync(id);
        if (passenger is null) return NotFound();
        if (await context.Tickets.AnyAsync(t => t.PassengerId == id))
            return Conflict("Сначала отмените билеты пассажира.");
        context.Passengers.Remove(passenger);
        await context.SaveChangesAsync();
        return NoContent();
    }

    private static void Copy(Passenger source, Passenger destination)
    {
        destination.FirstName = source.FirstName;
        destination.LastName = source.LastName;
        destination.PassportNumber = source.PassportNumber;
        destination.Email = source.Email;
        destination.Phone = source.Phone;
        destination.DateOfBirth = DateTime.SpecifyKind(source.DateOfBirth, DateTimeKind.Utc);
    }
}
