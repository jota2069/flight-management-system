namespace FlightManagement.Api.Models;

public class FlightCrewAssignment
{
    public int Id { get; set; }
    public string Role { get; set; } = string.Empty;

    public int FlightId { get; set; }
    public Flight Flight { get; set; } = null!;

    public int FlightAttendantId { get; set; }
    public FlightAttendant FlightAttendant { get; set; } = null!;
}