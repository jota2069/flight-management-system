using FlightManagement.Api.Models;
using FlightManagement.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FlightManagement.Api.Data;

public class AppDbContext : FlightIdentityDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<CacheRevision> CacheRevisions => Set<CacheRevision>();
    public DbSet<Flight> Flights => Set<Flight>();
    public DbSet<Passenger> Passengers => Set<Passenger>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<FlightAttendant> FlightAttendants => Set<FlightAttendant>();
    public DbSet<FlightCrewAssignment> FlightCrewAssignments => Set<FlightCrewAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<CacheRevision>().HasData(new CacheRevision { Id = 1, Version = 0 });
        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.Property(t => t.NormalizedSeatNumber)
                .HasComputedColumnSql("upper(btrim(\"SeatNumber\"))", stored: true);
            entity.HasIndex(t => new { t.FlightId, t.NormalizedSeatNumber }).IsUnique();
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Tickets_PositivePrice", "\"Price\" > 0");
                t.HasCheckConstraint("CK_Tickets_SeatLabel", "btrim(\"SeatNumber\") ~ '^[A-Za-z0-9]{1,8}$'");
            });
            entity.HasOne(t => t.Flight).WithMany(f => f.Tickets).HasForeignKey(t => t.FlightId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(t => t.Passenger).WithMany(p => p.Tickets).HasForeignKey(t => t.PassengerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(t => t.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(t => t.Price).HasColumnType("decimal(18,2)");
        });
        modelBuilder.Entity<Flight>().ToTable(t =>
            t.HasCheckConstraint("CK_Flights_PositiveSeats", "\"TotalSeats\" > 0"));
        modelBuilder.Entity<Passenger>().HasOne<IdentityUser>().WithMany()
            .HasForeignKey(p => p.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<FlightCrewAssignment>(entity =>
        {
            entity.HasOne(a => a.Flight).WithMany(f => f.CrewAssignments).HasForeignKey(a => a.FlightId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(a => a.FlightAttendant).WithMany(fa => fa.CrewAssignments)
                .HasForeignKey(a => a.FlightAttendantId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
