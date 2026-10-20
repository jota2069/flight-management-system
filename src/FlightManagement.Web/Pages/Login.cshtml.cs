using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlightManagement.Web.Pages;

public class LoginModel(SignInManager<IdentityUser> signInManager) : PageModel
{
    [BindProperty] public string Username { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var result = await signInManager.PasswordSignInAsync(Username, Password,
            isPersistent: false, lockoutOnFailure: true);
        if (result.Succeeded)
            return RedirectToPage("/Index");
        ErrorMessage = "Неверные данные входа или вход временно недоступен.";
        return Page();
    }
}
