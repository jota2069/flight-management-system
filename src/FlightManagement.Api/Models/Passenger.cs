using System.Text.Json.Serialization;

namespace FlightManagement.Api.Models;

public class Passenger
{
    [JsonIgnore] public string? OwnerUserId { get; set; }
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PassportNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }

    [JsonIgnore]
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
