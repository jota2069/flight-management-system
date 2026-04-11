using System.Text;
using System.Text.Json;

namespace FlightManagement.Web.Services;

public delegate void ApiLogHandler(string message);

public class FlightApiService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public event ApiLogHandler? OnLog;

    public FlightApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    private void Subscribe(out ApiLogHandler logger, out ApiLogHandler fileLogger)
    {
        logger = message => Console.WriteLine($"[ЛОГ]: {message}");
        fileLogger = message => File.AppendAllText("api_log.txt", $"{DateTime.Now:HH:mm:ss} {message}\n");
        OnLog += logger;
        OnLog += fileLogger;
    }

    private void Unsubscribe(ApiLogHandler logger, ApiLogHandler fileLogger)
    {
        OnLog -= logger;
        OnLog -= fileLogger;
    }

    // ===== FLIGHTS =====

    public async Task<List<FlightDto>> GetFlightsAsync()
    {
        Subscribe(out ApiLogHandler logger, out ApiLogHandler fileLogger);
        try
        {
            OnLog?.Invoke("Запрос списка рейсов...");
            HttpResponseMessage response = await _httpClient.GetAsync("api/Flights");
            response.EnsureSuccessStatusCode();
            string content = await response.Content.ReadAsStringAsync();
            List<FlightDto>? flights = JsonSerializer.Deserialize<List<FlightDto>>(content, _jsonOptions);
            OnLog?.Invoke($"Получено рейсов: {flights?.Count}");
            return flights ?? new List<FlightDto>();
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"Ошибка: {ex.Message}");
            return new List<FlightDto>();
        }
        finally { Unsubscribe(logger, fileLogger); }
    }

    public async Task<FlightDto?> GetFlightByIdAsync(int id)
    {
        Subscribe(out ApiLogHandler logger, out ApiLogHandler fileLogger);
        try
        {
            OnLog?.Invoke($"Запрос рейса id={id}...");
            HttpResponseMessage response = await _httpClient.GetAsync($"api/Flights/{id}");
            if (!response.IsSuccessStatusCode) return null;
            string content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<FlightDto>(content, _jsonOptions);
        }
        catch (Exception ex) { OnLog?.Invoke($"Ошибка: {ex.Message}"); return null; }
        finally { Unsubscribe(logger, fileLogger); }
    }

    public async Task<bool> CreateFlightAsync(FlightDto flight)
    {
        Subscribe(out ApiLogHandler logger, out ApiLogHandler fileLogger);
        Func<FlightDto, string> serialize = f => JsonSerializer.Serialize(f);
        try
        {
            OnLog?.Invoke($"Создание рейса {flight.FlightNumber}...");
            StringContent body = new StringContent(serialize(flight), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _httpClient.PostAsync("api/Flights", body);
            OnLog?.Invoke(response.IsSuccessStatusCode ? "Рейс создан." : "Ошибка создания.");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { OnLog?.Invoke($"Ошибка: {ex.Message}"); return false; }
        finally { Unsubscribe(logger, fileLogger); }
    }

    public async Task<bool> UpdateFlightAsync(int id, FlightDto flight)
    {
        Subscribe(out ApiLogHandler logger, out ApiLogHandler fileLogger);
        Func<FlightDto, string> serialize = f => JsonSerializer.Serialize(f);
        try
        {
            OnLog?.Invoke($"Обновление рейса {id}...");
            StringContent body = new StringContent(serialize(flight), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _httpClient.PutAsync($"api/Flights/{id}", body);
            OnLog?.Invoke(response.IsSuccessStatusCode ? "Рейс обновлён." : "Ошибка обновления.");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { OnLog?.Invoke($"Ошибка: {ex.Message}"); return false; }
        finally { Unsubscribe(logger, fileLogger); }
    }

    public async Task<bool> DeleteFlightAsync(int id)
    {
        Subscribe(out ApiLogHandler logger, out ApiLogHandler fileLogger);
        Action<int> logDelete = flightId => Console.WriteLine($"[ЛОГ]: Удаление рейса id={flightId}");
        logDelete(id);
        try
        {
            OnLog?.Invoke($"Удаление рейса {id}...");
            HttpResponseMessage response = await _httpClient.DeleteAsync($"api/Flights/{id}");
            OnLog?.Invoke(response.IsSuccessStatusCode ? "Рейс удалён." : "Ошибка удаления.");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { OnLog?.Invoke($"Ошибка: {ex.Message}"); return false; }
        finally { Unsubscribe(logger, fileLogger); }
    }

    // ===== PASSENGERS =====

    public async Task<List<PassengerDto>> GetPassengersAsync()
    {
        Subscribe(out ApiLogHandler logger, out ApiLogHandler fileLogger);
        try
        {
            OnLog?.Invoke("Запрос списка пассажиров...");
            HttpResponseMessage response = await _httpClient.GetAsync("api/Passengers");
            response.EnsureSuccessStatusCode();
            string content = await response.Content.ReadAsStringAsync();
            List<PassengerDto>? passengers = JsonSerializer.Deserialize<List<PassengerDto>>(content, _jsonOptions);
            OnLog?.Invoke($"Получено пассажиров: {passengers?.Count}");
            return passengers ?? new List<PassengerDto>();
        }
        catch (Exception ex) { OnLog?.Invoke($"Ошибка: {ex.Message}"); return new List<PassengerDto>(); }
        finally { Unsubscribe(logger, fileLogger); }
    }

    public async Task<PassengerDto?> CreatePassengerAsync(PassengerDto passenger)
    {
        Subscribe(out ApiLogHandler logger, out ApiLogHandler fileLogger);
        Func<PassengerDto, string> serialize = p => JsonSerializer.Serialize(p);
        try
        {
            OnLog?.Invoke($"Создание пассажира {passenger.LastName}...");
            StringContent body = new StringContent(serialize(passenger), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _httpClient.PostAsync("api/Passengers", body);
            if (!response.IsSuccessStatusCode) return null;
            string content = await response.Content.ReadAsStringAsync();
            OnLog?.Invoke("Пассажир создан.");
            return JsonSerializer.Deserialize<PassengerDto>(content, _jsonOptions);
        }
        catch (Exception ex) { OnLog?.Invoke($"Ошибка: {ex.Message}"); return null; }
        finally { Unsubscribe(logger, fileLogger); }
    }

    // ===== TICKETS =====

    public async Task<List<TicketDto>> GetTicketsAsync()
    {
        Subscribe(out ApiLogHandler logger, out ApiLogHandler fileLogger);
        try
        {
            OnLog?.Invoke("Запрос списка билетов...");
            HttpResponseMessage response = await _httpClient.GetAsync("api/Tickets");
            response.EnsureSuccessStatusCode();
            string content = await response.Content.ReadAsStringAsync();
            List<TicketDto>? tickets = JsonSerializer.Deserialize<List<TicketDto>>(content, _jsonOptions);
            OnLog?.Invoke($"Получено билетов: {tickets?.Count}");
            return tickets ?? new List<TicketDto>();
        }
        catch (Exception ex) { OnLog?.Invoke($"Ошибка: {ex.Message}"); return new List<TicketDto>(); }
        finally { Unsubscribe(logger, fileLogger); }
    }

    public async Task<bool> CreateTicketAsync(TicketDto ticket)
    {
        Subscribe(out ApiLogHandler logger, out ApiLogHandler fileLogger);
        Func<TicketDto, string> serialize = t => JsonSerializer.Serialize(t);
        try
        {
            OnLog?.Invoke($"Бронирование билета на рейс {ticket.FlightId}...");
            StringContent body = new StringContent(serialize(ticket), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _httpClient.PostAsync("api/Tickets", body);
            OnLog?.Invoke(response.IsSuccessStatusCode ? "Билет забронирован." : "Ошибка бронирования.");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { OnLog?.Invoke($"Ошибка: {ex.Message}"); return false; }
        finally { Unsubscribe(logger, fileLogger); }
    }

    public async Task<bool> DeleteTicketAsync(int id)
    {
        Subscribe(out ApiLogHandler logger, out ApiLogHandler fileLogger);
        Action<int> logCancel = ticketId => Console.WriteLine($"[ЛОГ]: Отмена билета id={ticketId}");
        logCancel(id);
        try
        {
            OnLog?.Invoke($"Отмена билета {id}...");
            HttpResponseMessage response = await _httpClient.DeleteAsync($"api/Tickets/{id}");
            OnLog?.Invoke(response.IsSuccessStatusCode ? "Билет отменён." : "Ошибка отмены.");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { OnLog?.Invoke($"Ошибка: {ex.Message}"); return false; }
        finally { Unsubscribe(logger, fileLogger); }
    }
}

// ===== DTOs =====

public class FlightDto
{
    public int Id { get; set; }
    public string FlightNumber { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
    public int TotalSeats { get; set; }
    public string Status { get; set; } = "Scheduled";
}

public class PassengerDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PassportNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
}

public class TicketDto
{
    public int Id { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Status { get; set; } = "Confirmed";
    public int FlightId { get; set; }
    public int PassengerId { get; set; }
    public FlightDto? Flight { get; set; }
    public PassengerDto? Passenger { get; set; }
}