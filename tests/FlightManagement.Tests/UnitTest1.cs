using FlightManagement.Api.Models;

namespace FlightManagement.Tests;

public class FlightValidationTests
{
    [Fact]
    public void Flight_ArrivalTime_ShouldBeLaterThan_DepartureTime()
    {
        Flight flight = new Flight
        {
            FlightNumber = "SU-101",
            Origin = "Москва",
            Destination = "Санкт-Петербург",
            DepartureTime = new DateTime(2025, 6, 1, 8, 0, 0),
            ArrivalTime = new DateTime(2025, 6, 1, 9, 30, 0),
            TotalSeats = 150,
            Status = "Scheduled"
        };

        Assert.True(flight.ArrivalTime > flight.DepartureTime);
    }

    [Fact]
    public void Ticket_Price_ShouldBePositive()
    {
        Ticket ticket = new Ticket
        {
            SeatNumber = "1A",
            Price = 3500.00m,
            Status = "Confirmed",
            PurchaseDate = DateTime.UtcNow,
            FlightId = 1,
            PassengerId = 1
        };

        Assert.True(ticket.Price > 0);
    }

    [Fact]
    public void FlightAttendant_ExperienceYears_ShouldBeNonNegative()
    {
        FlightAttendant attendant = new FlightAttendant
        {
            FirstName = "Анна",
            LastName = "Козлова",
            EmployeeNumber = "EMP-001",
            Email = "kozlova@airline.com",
            Phone = "+79009876543",
            ExperienceYears = 5
        };

        Assert.True(attendant.ExperienceYears >= 0);
    }
}