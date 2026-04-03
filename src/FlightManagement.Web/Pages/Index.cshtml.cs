using FlightManagement.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlightManagement.Web.Pages;

[Authorize]
public class IndexModel : PageModel
{
    private readonly FlightApiService _apiService;

    public List<FlightDto> Flights { get; set; } = new List<FlightDto>();

    public IndexModel(FlightApiService apiService)
    {
        _apiService = apiService;
    }

    public async Task OnGetAsync()
    {
        Flights = await _apiService.GetFlightsAsync();
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync("Cookies");
        return RedirectToPage("/Login");
    }
}