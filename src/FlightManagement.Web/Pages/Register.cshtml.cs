using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlightManagement.Web.Pages;

public class RegisterModel(UserManager<IdentityUser> users) : PageModel
{
    [BindProperty] public string Username { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var result = await users.CreateAsync(new IdentityUser { UserName = Username }, Password);
        if (result.Succeeded) return RedirectToPage("/Login");
        ErrorMessage = "Регистрация не выполнена. Проверьте логин и сложность пароля (от 12 символов).";
        return Page();
    }
}
