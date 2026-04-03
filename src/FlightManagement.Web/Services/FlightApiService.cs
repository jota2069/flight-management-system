using System.IO;
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

    public async Task<List<FlightDto>> GetFlightsAsync()
    {
        ApiLogHandler? logger = null;
        logger = message => Console.WriteLine($"[ЛОГ]: {message}");
        OnLog += logger;

        ApiLogHandler? fileLogger = null;
        fileLogger = message => File.AppendAllText("api_log.txt", $"{DateTime.Now:HH:mm:ss} {message}\n");
        OnLog += fileLogger;

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
        finally
        {
            OnLog -= logger;
            OnLog -= fileLogger;
        }
    }

    public async Task<FlightDto?> GetFlightByIdAsync(int id)
    {
        ApiLogHandler? logger = null;
        logger = message => Console.WriteLine($"[ЛОГ]: {message}");
        OnLog += logger;

        ApiLogHandler? fileLogger = null;
        fileLogger = message => File.AppendAllText("api_log.txt", $"{DateTime.Now:HH:mm:ss} {message}\n");
        OnLog += fileLogger;

        try
        {
            OnLog?.Invoke($"Запрос рейса с id={id}...");
            HttpResponseMessage response = await _httpClient.GetAsync($"api/Flights/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            string content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<FlightDto>(content, _jsonOptions);
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"Ошибка: {ex.Message}");
            return null;
        }
        finally
        {
            OnLog -= logger;
            OnLog -= fileLogger;
        }
    }

    public async Task<bool> CreateFlightAsync(FlightDto flight)
    {
        ApiLogHandler? logger = null;
        logger = message => Console.WriteLine($"[ЛОГ]: {message}");
        OnLog += logger;

        ApiLogHandler? fileLogger = null;
        fileLogger = message => File.AppendAllText("api_log.txt", $"{DateTime.Now:HH:mm:ss} {message}\n");
        OnLog += fileLogger;

        Func<FlightDto, string> serialize = f => JsonSerializer.Serialize(f);

        try
        {
            OnLog?.Invoke($"Создание рейса {flight.FlightNumber}...");
            StringContent body = new StringContent(serialize(flight), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _httpClient.PostAsync("api/Flights", body);
            OnLog?.Invoke(response.IsSuccessStatusCode ? "Рейс успешно создан." : "Ошибка создания рейса.");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"Ошибка: {ex.Message}");
            return false;
        }
        finally
        {
            OnLog -= logger;
            OnLog -= fileLogger;
        }
    }

    public async Task<bool> UpdateFlightAsync(int id, FlightDto flight)
    {
        ApiLogHandler? logger = null;
        logger = message => Console.WriteLine($"[ЛОГ]: {message}");
        OnLog += logger;

        ApiLogHandler? fileLogger = null;
        fileLogger = message => File.AppendAllText("api_log.txt", $"{DateTime.Now:HH:mm:ss} {message}\n");
        OnLog += fileLogger;

        Func<FlightDto, string> serialize = f => JsonSerializer.Serialize(f);

        try
        {
            OnLog?.Invoke($"Обновление рейса {id}...");
            StringContent body = new StringContent(serialize(flight), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _httpClient.PutAsync($"api/Flights/{id}", body);
            OnLog?.Invoke(response.IsSuccessStatusCode ? "Рейс обновлён." : "Ошибка обновления.");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"Ошибка: {ex.Message}");
            return false;
        }
        finally
        {
            OnLog -= logger;
            OnLog -= fileLogger;
        }
    }

    public async Task<bool> DeleteFlightAsync(int id)
    {
        ApiLogHandler? logger = null;
        logger = message => Console.WriteLine($"[ЛОГ]: {message}");
        OnLog += logger;

        ApiLogHandler? fileLogger = null;
        fileLogger = message => File.AppendAllText("api_log.txt", $"{DateTime.Now:HH:mm:ss} {message}\n");
        OnLog += fileLogger;

        Action<int> logDelete = flightId => Console.WriteLine($"[ЛОГ]: Удаление рейса с id={flightId}");
        logDelete(id);

        try
        {
            OnLog?.Invoke($"Удаление рейса {id}...");
            HttpResponseMessage response = await _httpClient.DeleteAsync($"api/Flights/{id}");
            OnLog?.Invoke(response.IsSuccessStatusCode ? "Рейс удалён." : "Ошибка удаления.");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"Ошибка: {ex.Message}");
            return false;
        }
        finally
        {
            OnLog -= logger;
            OnLog -= fileLogger;
        }
    }
}

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