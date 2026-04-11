using FlightManagement.Api.Models;

namespace FlightManagement.Api.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext context)
    {
        if (context.Flights.Any())
        {
            return;
        }

        List<Flight> flights = new List<Flight>
{
    // В Нальчик (5 рейсов)
    new Flight { FlightNumber = "RA-101", Origin = "Москва", Destination = "Нальчик", DepartureTime = new DateTime(2025, 6, 1, 6, 0, 0, DateTimeKind.Utc), ArrivalTime = new DateTime(2025, 6, 1, 9, 30, 0, DateTimeKind.Utc), TotalSeats = 120, Status = "Scheduled" },
    new Flight { FlightNumber = "RA-102", Origin = "Санкт-Петербург", Destination = "Нальчик", DepartureTime = new DateTime(2025, 6, 3, 10, 0, 0, DateTimeKind.Utc), ArrivalTime = new DateTime(2025, 6, 3, 14, 0, 0, DateTimeKind.Utc), TotalSeats = 100, Status = "Scheduled" },
    new Flight { FlightNumber = "RA-103", Origin = "Казань", Destination = "Нальчик", DepartureTime = new DateTime(2025, 6, 5, 8, 30, 0, DateTimeKind.Utc), ArrivalTime = new DateTime(2025, 6, 5, 11, 45, 0, DateTimeKind.Utc), TotalSeats = 80, Status = "Scheduled" },
    new Flight { FlightNumber = "RA-104", Origin = "Екатеринбург", Destination = "Нальчик", DepartureTime = new DateTime(2025, 6, 7, 14, 0, 0, DateTimeKind.Utc), ArrivalTime = new DateTime(2025, 6, 7, 18, 30, 0, DateTimeKind.Utc), TotalSeats = 150, Status = "Scheduled" },
    new Flight { FlightNumber = "RA-105", Origin = "Новосибирск", Destination = "Нальчик", DepartureTime = new DateTime(2025, 6, 9, 7, 0, 0, DateTimeKind.Utc), ArrivalTime = new DateTime(2025, 6, 9, 13, 0, 0, DateTimeKind.Utc), TotalSeats = 90, Status = "Scheduled" },

    // Из Нальчика (5 рейсов)
    new Flight { FlightNumber = "RA-201", Origin = "Нальчик", Destination = "Москва", DepartureTime = new DateTime(2025, 6, 2, 11, 0, 0, DateTimeKind.Utc), ArrivalTime = new DateTime(2025, 6, 2, 14, 30, 0, DateTimeKind.Utc), TotalSeats = 120, Status = "Scheduled" },
    new Flight { FlightNumber = "RA-202", Origin = "Нальчик", Destination = "Санкт-Петербург", DepartureTime = new DateTime(2025, 6, 4, 15, 0, 0, DateTimeKind.Utc), ArrivalTime = new DateTime(2025, 6, 4, 19, 0, 0, DateTimeKind.Utc), TotalSeats = 100, Status = "Scheduled" },
    new Flight { FlightNumber = "RA-203", Origin = "Нальчик", Destination = "Казань", DepartureTime = new DateTime(2025, 6, 6, 9, 0, 0, DateTimeKind.Utc), ArrivalTime = new DateTime(2025, 6, 6, 12, 15, 0, DateTimeKind.Utc), TotalSeats = 80, Status = "Scheduled" },
    new Flight { FlightNumber = "RA-204", Origin = "Нальчик", Destination = "Екатеринбург", DepartureTime = new DateTime(2025, 6, 8, 16, 0, 0, DateTimeKind.Utc), ArrivalTime = new DateTime(2025, 6, 8, 20, 30, 0, DateTimeKind.Utc), TotalSeats = 150, Status = "Scheduled" },
    new Flight { FlightNumber = "RA-205", Origin = "Нальчик", Destination = "Новосибирск", DepartureTime = new DateTime(2025, 6, 10, 6, 30, 0, DateTimeKind.Utc), ArrivalTime = new DateTime(2025, 6, 10, 12, 30, 0, DateTimeKind.Utc), TotalSeats = 90, Status = "Scheduled" },

    // Остальные российские (10 рейсов)
    new Flight { FlightNumber = "SU-301", Origin = "Москва", Destination = "Санкт-Петербург", DepartureTime = new DateTime(2025, 6, 1, 8, 0, 0, DateTimeKind.Utc), ArrivalTime = new DateTime(2025, 6, 1, 9, 30, 0, DateTimeKind.Utc), TotalSeats = 150, Status = "Scheduled" },
    new Flight { FlightNumber = "SU-302", Origin = "Москва", Destination = "Сочи", DepartureTime = new DateTime(2025, 6, 2, 9, 0, 0, DateTimeKind.Utc), ArrivalTime = new DateTime(2025, 6, 2, 11, 30, 0, DateTimeKind.Utc), TotalSeats = 180, Status = "Scheduled" },
    new Flight { FlightNumber = "SU-303", Origin = "Москва", Destination = "Казань", DepartureTime = new DateTime(2025, 6, 3, 7, 0, 0, DateTimeKind.Utc), ArrivalTime = new DateTime(2025, 6, 3, 8, 30, 0, DateTimeKind.Utc), TotalSeats = 130, Status = "Scheduled" },
    new Flight { FlightNumber = "SU-304", Origin = "Санкт-Петербург", Destination = "Сочи", DepartureTime = new DateTime(2025, 6, 4, 10, 0, 0, DateTimeKind.Utc), ArrivalTime = new DateTime(2025, 6, 4, 12, 45, 0, DateTimeKind.Utc), TotalSeats = 160, Status = "Scheduled" },
    new Flight { FlightNumber = "SU-305", Origin = "Екатеринбург", Destination = "Москва", DepartureTime = new DateTime(2025, 6, 5, 6, 0, 0, DateTimeKind.Utc), ArrivalTime = new DateTime(2025, 6, 5, 8, 30, 0, DateTimeKind.Utc), TotalSeats = 140, Status = "Scheduled" },
    new Flight { FlightNumber = "SU-306", Origin = "Новосибирск", Destination = "Москва", DepartureTime = new DateTime(2025, 6, 6, 5, 0, 0, DateTimeKind.Utc), ArrivalTime = new DateTime(2025, 6, 6, 9, 0, 0, DateTimeKind.Utc), TotalSeats = 200, Status = "Scheduled" },
    new Flight { FlightNumber = "SU-307", Origin = "Казань", Destination = "Санкт-Петербург", DepartureTime = new DateTime(2025, 6, 7, 11, 0, 0, DateTimeKind.Utc), ArrivalTime = new DateTime(2025, 6, 7, 13, 0, 0, DateTimeKind.Utc), TotalSeats = 110, Status = "Scheduled" },
    new Flight { FlightNumber = "SU-308", Origin = "Сочи", Destination = "Москва", DepartureTime = new DateTime(2025, 6, 8, 13, 0, 0, DateTimeKind.Utc), ArrivalTime = new DateTime(2025, 6, 8, 15, 30, 0, DateTimeKind.Utc), TotalSeats = 175, Status = "Scheduled" },
    new Flight { FlightNumber = "SU-309", Origin = "Владивосток", Destination = "Москва", DepartureTime = new DateTime(2025, 6, 9, 1, 0, 0, DateTimeKind.Utc), ArrivalTime = new DateTime(2025, 6, 9, 10, 0, 0, DateTimeKind.Utc), TotalSeats = 220, Status = "Scheduled" },
    new Flight { FlightNumber = "SU-310", Origin = "Москва", Destination = "Владивосток", DepartureTime = new DateTime(2025, 6, 10, 11, 0, 0, DateTimeKind.Utc), ArrivalTime = new DateTime(2025, 6, 10, 20, 0, 0, DateTimeKind.Utc), TotalSeats = 220, Status = "Scheduled" },
};

        context.Flights.AddRange(flights);

        List<Passenger> passengers = new List<Passenger>
        {
            new Passenger
            {
                FirstName = "Иван",
                LastName = "Иванов",
                PassportNumber = "1234567890",
                Email = "ivanov@example.com",
                Phone = "+79001234567",
                DateOfBirth = new DateTime(1990, 5, 15, 0, 0, 0, DateTimeKind.Utc)
            },
            new Passenger
            {
                FirstName = "Мария",
                LastName = "Петрова",
                PassportNumber = "0987654321",
                Email = "petrova@example.com",
                Phone = "+79007654321",
                DateOfBirth = new DateTime(1985, 3, 22, 0, 0, 0, DateTimeKind.Utc)
            },
            new Passenger
            {
                FirstName = "Алексей",
                LastName = "Сидоров",
                PassportNumber = "1122334455",
                Email = "sidorov@example.com",
                Phone = "+79001122334",
                DateOfBirth = new DateTime(1995, 8, 10, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        context.Passengers.AddRange(passengers);

        List<FlightAttendant> attendants = new List<FlightAttendant>
        {
            new FlightAttendant
            {
                FirstName = "Анна",
                LastName = "Козлова",
                EmployeeNumber = "EMP-001",
                Email = "kozlova@airline.com",
                Phone = "+79009876543",
                ExperienceYears = 5
            },
            new FlightAttendant
            {
                FirstName = "Дмитрий",
                LastName = "Новиков",
                EmployeeNumber = "EMP-002",
                Email = "novikov@airline.com",
                Phone = "+79008765432",
                ExperienceYears = 8
            }
        };

        context.FlightAttendants.AddRange(attendants);
        context.SaveChanges();

        List<Ticket> tickets = new List<Ticket>
        {
            new Ticket
            {
                SeatNumber = "1A",
                Price = 3500.00m,
                Status = "Confirmed",
                PurchaseDate = DateTime.UtcNow,
                FlightId = flights[0].Id,
                PassengerId = passengers[0].Id
            },
            new Ticket
            {
                SeatNumber = "2B",
                Price = 3500.00m,
                Status = "Confirmed",
                PurchaseDate = DateTime.UtcNow,
                FlightId = flights[0].Id,
                PassengerId = passengers[1].Id
            },
            new Ticket
            {
                SeatNumber = "5C",
                Price = 4200.00m,
                Status = "Confirmed",
                PurchaseDate = DateTime.UtcNow,
                FlightId = flights[1].Id,
                PassengerId = passengers[2].Id
            }
        };

        context.Tickets.AddRange(tickets);

        List<FlightCrewAssignment> assignments = new List<FlightCrewAssignment>
        {
            new FlightCrewAssignment
            {
                Role = "Старший бортпроводник",
                FlightId = flights[0].Id,
                FlightAttendantId = attendants[0].Id
            },
            new FlightCrewAssignment
            {
                Role = "Бортпроводник",
                FlightId = flights[0].Id,
                FlightAttendantId = attendants[1].Id
            },
            new FlightCrewAssignment
            {
                Role = "Старший бортпроводник",
                FlightId = flights[1].Id,
                FlightAttendantId = attendants[1].Id
            }
        };

        context.FlightCrewAssignments.AddRange(assignments);
        context.SaveChanges();
    }
}