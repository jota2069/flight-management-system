using FlightManagement.Api.Data;
using FlightManagement.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlightManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FlightAttendantsController : ControllerBase
{
    private readonly AppDbContext _context;

    public FlightAttendantsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<FlightAttendant>>> GetAll()
    {
        List<FlightAttendant> attendants = await _context.FlightAttendants
            .Include(fa => fa.CrewAssignments)
            .ToListAsync();

        return Ok(attendants);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FlightAttendant>> GetById(int id)
    {
        FlightAttendant? attendant = await _context.FlightAttendants
            .Include(fa => fa.CrewAssignments)
            .FirstOrDefaultAsync(fa => fa.Id == id);

        if (attendant is null)
        {
            return NotFound();
        }

        return Ok(attendant);
    }

    [HttpPost]
    public async Task<ActionResult<FlightAttendant>> Create(FlightAttendant attendant)
    {
        _context.FlightAttendants.Add(attendant);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = attendant.Id }, attendant);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> Update(int id, FlightAttendant attendant)
    {
        if (id != attendant.Id)
        {
            return BadRequest();
        }

        _context.Entry(attendant).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _context.FlightAttendants.AnyAsync(fa => fa.Id == id))
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
        FlightAttendant? attendant = await _context.FlightAttendants.FindAsync(id);

        if (attendant is null)
        {
            return NotFound();
        }

        _context.FlightAttendants.Remove(attendant);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}