using FlightManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FlightManagement.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Flight> Flights => Set<Flight>();
    public DbSet<Passenger> Passengers => Set<Passenger>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<FlightAttendant> FlightAttendants => Set<FlightAttendant>();
    public DbSet<FlightCrewAssignment> FlightCrewAssignments => Set<FlightCrewAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasOne(t => t.Flight)
                .WithMany(f => f.Tickets)
                .HasForeignKey(t => t.FlightId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(t => t.Passenger)
                .WithMany(p => p.Tickets)
                .HasForeignKey(t => t.PassengerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(t => t.Price)
                .HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<FlightCrewAssignment>(entity =>
        {
            entity.HasOne(a => a.Flight)
                .WithMany(f => f.CrewAssignments)
                .HasForeignKey(a => a.FlightId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.FlightAttendant)
                .WithMany(fa => fa.CrewAssignments)
                .HasForeignKey(a => a.FlightAttendantId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}