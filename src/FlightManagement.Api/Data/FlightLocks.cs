using FlightManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FlightManagement.Api.Data;

internal static class FlightLocks
{
    // Lock ordering is identical for booking, moving, cancellation and capacity edits.
    // PostgreSQL READ COMMITTED gives subsequent queries a fresh snapshot after waiting.
    public static async Task<Dictionary<int, Flight>> Acquire(AppDbContext db, params int[] ids)
    {
        var flights = new Dictionary<int, Flight>();
        foreach (int id in ids.Distinct().Order())
        {
            var rows = await db.Flights.FromSqlInterpolated(
                $"SELECT * FROM \"Flights\" WHERE \"Id\" = {id} FOR UPDATE").ToListAsync();
            if (rows.Count == 1) flights.Add(id, rows[0]);
        }
        return flights;
    }
}
