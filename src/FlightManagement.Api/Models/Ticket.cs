using System.Text.Json.Serialization;

namespace FlightManagement.Api.Models;

public class Ticket
{
    [JsonIgnore] public string? OwnerUserId { get; set; }
    public int Id { get; set; }
    [JsonIgnore] public string NormalizedSeatNumber { get; private set; } = string.Empty;
    public string SeatNumber { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Status { get; set; } = "Confirmed";
    public DateTime PurchaseDate { get; set; }

    public int FlightId { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public Flight Flight { get; set; } = null!;

    public int PassengerId { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public Passenger Passenger { get; set; } = null!;
}
