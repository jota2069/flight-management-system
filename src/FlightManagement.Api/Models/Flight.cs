using System.Text.Json.Serialization;

namespace FlightManagement.Api.Models;

public class Flight
{
    public int Id { get; set; }
    public string FlightNumber { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
    public int TotalSeats { get; set; }
    public string Status { get; set; } = "Scheduled";

    [JsonIgnore]
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    [JsonIgnore]
    public ICollection<FlightCrewAssignment> CrewAssignments { get; set; } = new List<FlightCrewAssignment>();
}
