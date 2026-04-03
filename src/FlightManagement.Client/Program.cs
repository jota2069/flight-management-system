using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FlightManagement.Client;

public delegate void LogMessageHandler(string message);

public class ApiClient
{
    public event LogMessageHandler? OnLog;
    private readonly HttpClient _httpClient;

    public ApiClient()
    {
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri("http://localhost:8080/");
    }

    public async Task GetFlightsAsync()
    {
        OnLog?.Invoke("Связь с сервером. Получение актуального расписания...");
        
        try 
        {
            HttpResponseMessage response = await _httpClient.GetAsync("api/Flights");
            response.EnsureSuccessStatusCode();
            
            string content = await response.Content.ReadAsStringAsync();
            
            JsonSerializerOptions options = new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            };
            
            List<FlightDto>? flights = JsonSerializer.Deserialize<List<FlightDto>>(content, options);

            OnLog?.Invoke($"Загрузка завершена. Найдено рейсов: {flights?.Count}\n");
            
            if (flights != null)
            {
                foreach (FlightDto flight in flights) 
                {
                    // Переводим статус на русский язык для красоты
                    string displayStatus = flight.Status == "Scheduled" ? "По расписанию" : flight.Status;
                    
                    Console.WriteLine($"✈️ Рейс: {flight.FlightNumber} | Маршрут: {flight.Origin} -> {flight.Destination} | Статус: {displayStatus}");
                }
            }
        } 
        catch (Exception ex) 
        {
            OnLog?.Invoke($"Сбой сети. Ошибка: {ex.Message}");
        }
    }
}

public class FlightDto 
{
    public string FlightNumber { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Убрали "===" как ты и просил
        Console.WriteLine("Терминал Аэропорта (Система Управления Рейсами)");
        
        ApiClient client = new ApiClient();
        client.OnLog += DisplaySystemLog;

        while (true) 
        {
            Console.WriteLine("\nГЛАВНОЕ МЕНЮ:");
            Console.WriteLine("1. Показать расписание рейсов");
            Console.WriteLine("2. Выход");
            Console.Write("Ваш выбор (1 или 2): ");
            
            string? choice = Console.ReadLine();

            if (choice == "1") 
            {
                Console.WriteLine();
                await client.GetFlightsAsync();
            } 
            else if (choice == "2") 
            {
                Console.WriteLine("Отключение от сервера...");
                break;
            }
            else
            {
                Console.WriteLine("Неизвестная команда. Попробуйте снова.");
            }
        }
    }

    private static void DisplaySystemLog(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan; // Сделал цвет более приятным (бирюзовым)
        Console.WriteLine($"[СИСТЕМА]: {message}");
        Console.ResetColor();
    }
}