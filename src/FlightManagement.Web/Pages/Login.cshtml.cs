using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace FlightManagement.Web.Pages;

public class LoginModel : PageModel
{
    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;

    public async Task<IActionResult> OnPostAsync()
    {
        if ((Username == "admin" && Password == "admin") ||
            (Username == "user" && Password == "user"))
        {
            string role = Username == "admin" ? "Admin" : "User";

            List<Claim> claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, Username),
                new Claim(ClaimTypes.Role, role)
            };

            ClaimsIdentity identity = new ClaimsIdentity(claims, "Cookies");
            ClaimsPrincipal principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("Cookies", principal);

            return role == "Admin" ? RedirectToPage("/Admin") : RedirectToPage("/Index");
        }

        ErrorMessage = "Неверный логин или пароль.";
        return Page();
    }
}