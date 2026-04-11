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
    public List<PassengerDto> Passengers { get; set; } = new List<PassengerDto>();
    public List<TicketDto> Tickets { get; set; } = new List<TicketDto>();
    public string Message { get; set; } = string.Empty;
    public bool IsError { get; set; } = false;

    [BindProperty]
    public FlightDto Input { get; set; } = new FlightDto();

    [BindProperty]
    public FlightDto EditInput { get; set; } = new FlightDto();

    public AdminModel(FlightApiService apiService)
    {
        _apiService = apiService;
    }

    public async Task OnGetAsync()
    {
        await LoadAllAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        Input.Status = "Scheduled";
        bool success = await _apiService.CreateFlightAsync(Input);

        Message = success ? "Рейс успешно добавлен." : "Ошибка при добавлении рейса.";
        IsError = !success;

        await LoadAllAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAsync()
    {
        bool success = await _apiService.UpdateFlightAsync(EditInput.Id, EditInput);

        Message = success
            ? $"Рейс {EditInput.FlightNumber} успешно обновлён."
            : "Ошибка при обновлении рейса.";
        IsError = !success;

        await LoadAllAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        bool success = await _apiService.DeleteFlightAsync(id);

        Message = success ? $"Рейс #{id} удалён." : $"Ошибка при удалении рейса #{id}.";
        IsError = !success;

        await LoadAllAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteTicketAsync(int ticketId)
    {
        bool success = await _apiService.DeleteTicketAsync(ticketId);

        Message = success ? $"Билет #{ticketId} удалён." : $"Ошибка при удалении билета #{ticketId}.";
        IsError = !success;

        await LoadAllAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync("Cookies");
        return RedirectToPage("/Login");
    }

    private async Task LoadAllAsync()
    {
        Flights = await _apiService.GetFlightsAsync();
        Passengers = await _apiService.GetPassengersAsync();
        Tickets = await _apiService.GetTicketsAsync();
    }
}