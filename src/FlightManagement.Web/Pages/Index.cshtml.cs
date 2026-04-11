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
    public List<TicketDto> MyTickets { get; set; } = new List<TicketDto>();
    public string Message { get; set; } = string.Empty;
    public bool IsError { get; set; } = false;

    [BindProperty]
    public BookingForm BookForm { get; set; } = new BookingForm();

    public IndexModel(FlightApiService apiService)
    {
        _apiService = apiService;
    }

    public async Task OnGetAsync()
    {
        Flights = await _apiService.GetFlightsAsync();
        await LoadMyTicketsAsync();
    }

    public async Task<IActionResult> OnPostBookAsync()
    {
        // 1. Создаём пассажира
        PassengerDto passenger = new PassengerDto
        {
            FirstName = BookForm.FirstName,
            LastName = BookForm.LastName,
            PassportNumber = BookForm.PassportNumber,
            Email = BookForm.Email,
            Phone = BookForm.Phone,
            DateOfBirth = BookForm.DateOfBirth
        };

        PassengerDto? created = await _apiService.CreatePassengerAsync(passenger);

        if (created is null)
        {
            Message = "Ошибка при создании пассажира. Проверьте данные.";
            IsError = true;
            Flights = await _apiService.GetFlightsAsync();
            await LoadMyTicketsAsync();
            return Page();
        }

        // 2. Создаём билет
        TicketDto ticket = new TicketDto
        {
            FlightId = BookForm.FlightId,
            PassengerId = created.Id,
            SeatNumber = BookForm.SeatNumber,
            Price = BookForm.Price,
            Status = "Confirmed"
        };

        bool success = await _apiService.CreateTicketAsync(ticket);

        if (success)
        {
            Message = $"Билет успешно забронирован! Рейс #{BookForm.FlightId}, место {BookForm.SeatNumber}.";
            IsError = false;
        }
        else
        {
            Message = "Ошибка при бронировании билета.";
            IsError = true;
        }

        Flights = await _apiService.GetFlightsAsync();
        await LoadMyTicketsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCancelTicketAsync(int ticketId)
    {
        bool success = await _apiService.DeleteTicketAsync(ticketId);

        Message = success
            ? $"Билет #{ticketId} успешно отменён."
            : $"Ошибка при отмене билета #{ticketId}.";
        IsError = !success;

        Flights = await _apiService.GetFlightsAsync();
        await LoadMyTicketsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync("Cookies");
        return RedirectToPage("/Login");
    }

    private async Task LoadMyTicketsAsync()
    {
        List<TicketDto> all = await _apiService.GetTicketsAsync();
        string? username = User.Identity?.Name;

        // Показываем билеты где имя пассажира совпадает с логином
        // Для user — все билеты (демо), для конкретного пользователя — фильтр по email
        MyTickets = username == "admin"
            ? all
            : all.Where(t => t.Passenger?.Email?.Contains(username ?? "") == true
                          || t.Passenger?.FirstName?.ToLower() == username?.ToLower()).ToList();

        // Если нет совпадений — показываем все (для демонстрации)
        if (MyTickets.Count == 0)
        {
            MyTickets = all;
        }
    }
}

public class BookingForm
{
    public int FlightId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PassportNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public decimal Price { get; set; }
}