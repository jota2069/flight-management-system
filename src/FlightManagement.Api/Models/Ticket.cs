namespace FlightManagement.Api.Models;

public class Ticket
{
    public int Id { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Status { get; set; } = "Confirmed";
    public DateTime PurchaseDate { get; set; }

    public int FlightId { get; set; }
    public Flight Flight { get; set; } = null!;

    public int PassengerId { get; set; }
    public Passenger Passenger { get; set; } = null!;
}