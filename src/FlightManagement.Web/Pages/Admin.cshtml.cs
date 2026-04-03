using FlightManagement.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlightManagement.Web.Pages;

[Authorize(Roles = "Admin")]
public class AdminModel : PageModel
{
    private readonly FlightApiService _apiService;

    public List<FlightDto> Flights { get; set; } = new List<FlightDto>();
    public string Message { get; set; } = string.Empty;
    public bool IsError { get; set; } = false;

    [BindProperty]
    public FlightDto Input { get; set; } = new FlightDto();

    public AdminModel(FlightApiService apiService)
    {
        _apiService = apiService;
    }

    public async Task OnGetAsync()
    {
        Flights = await _apiService.GetFlightsAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        Input.Status = "Scheduled";
        bool success = await _apiService.CreateFlightAsync(Input);

        if (success)
        {
            Message = "Рейс успешно добавлен.";
            IsError = false;
        }
        else
        {
            Message = "Ошибка при добавлении рейса.";
            IsError = true;
        }

        Flights = await _apiService.GetFlightsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        bool success = await _apiService.DeleteFlightAsync(id);

        if (success)
        {
            Message = $"Рейс #{id} удалён.";
            IsError = false;
        }
        else
        {
            Message = $"Ошибка при удалении рейса #{id}.";
            IsError = true;
        }

        Flights = await _apiService.GetFlightsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync("Cookies");
        return RedirectToPage("/Login");
    }
}