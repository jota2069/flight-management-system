using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FlightManagement.Security;

// Explicit local administration only. Never run during ordinary application startup.
public static class AdminProvisioning
{
    public static async Task<IdentityResult> Create(FlightIdentityDbContext db,
        UserManager<IdentityUser> users, RoleManager<IdentityRole> roles,
        string username, string password)
    {
        if (await users.FindByNameAsync(username) is not null)
            return IdentityResult.Failed(new IdentityError { Code = "ExistingAccount",
                Description = "Refusing to promote an existing account; choose a new administrator username." });
        if (password.Length > 128)
            return IdentityResult.Failed(new IdentityError { Code = "PasswordLength",
                Description = "Password must not exceed 128 characters." });
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (!await roles.RoleExistsAsync("Admin"))
        {
            var role = await roles.CreateAsync(new IdentityRole("Admin"));
            if (!role.Succeeded) return role;
        }
        var user = new IdentityUser { UserName = username };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded) return created;
        var added = await users.AddToRoleAsync(user, "Admin");
        if (!added.Succeeded) return added;
        await transaction.CommitAsync();
        return IdentityResult.Success;
    }

    public static string ReadPassword()
    {
        if (Console.IsInputRedirected) return Console.ReadLine() ?? "";
        var password = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return password.ToString(); }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0) password.Length--;
            }
            else if (!char.IsControl(key.KeyChar) && password.Length < 129) password.Append(key.KeyChar);
        }
    }
}
