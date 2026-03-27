namespace FlightManagement.Api.Models;

public class FlightAttendant
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int ExperienceYears { get; set; }

    public ICollection<FlightCrewAssignment> CrewAssignments { get; set; } = new List<FlightCrewAssignment>();
}