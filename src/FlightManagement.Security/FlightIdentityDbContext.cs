using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FlightManagement.Security;

// Both hosts use the same Identity tables. Only the API owns migrations.
public class FlightIdentityDbContext : IdentityDbContext
{
    public FlightIdentityDbContext(DbContextOptions options) : base(options) { }
}
