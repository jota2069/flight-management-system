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
            new Flight
            {
                FlightNumber = "SU-101",
                Origin = "Москва",
                Destination = "Санкт-Петербург",
                DepartureTime = new DateTime(2025, 6, 1, 8, 0, 0),
                ArrivalTime = new DateTime(2025, 6, 1, 9, 30, 0),
                TotalSeats = 150,
                Status = "Scheduled"
            },
            new Flight
            {
                FlightNumber = "SU-202",
                Origin = "Москва",
                Destination = "Казань",
                DepartureTime = new DateTime(2025, 6, 2, 12, 0, 0),
                ArrivalTime = new DateTime(2025, 6, 2, 13, 30, 0),
                TotalSeats = 120,
                Status = "Scheduled"
            },
            new Flight
            {
                FlightNumber = "SU-303",
                Origin = "Санкт-Петербург",
                Destination = "Сочи",
                DepartureTime = new DateTime(2025, 6, 3, 15, 0, 0),
                ArrivalTime = new DateTime(2025, 6, 3, 17, 30, 0),
                TotalSeats = 180,
                Status = "Scheduled"
            }
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
                DateOfBirth = new DateTime(1990, 5, 15)
            },
            new Passenger
            {
                FirstName = "Мария",
                LastName = "Петрова",
                PassportNumber = "0987654321",
                Email = "petrova@example.com",
                Phone = "+79007654321",
                DateOfBirth = new DateTime(1985, 3, 22)
            },
            new Passenger
            {
                FirstName = "Алексей",
                LastName = "Сидоров",
                PassportNumber = "1122334455",
                Email = "sidorov@example.com",
                Phone = "+79001122334",
                DateOfBirth = new DateTime(1995, 8, 10)
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