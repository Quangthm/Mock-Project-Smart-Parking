namespace SmartParking.FacilityService.API.Controllers;

public sealed record CreateTicketRequest(string Subject, string Message, Guid? BookingId);
public sealed record UpdateTicketRequest(string Status, string? ResolutionNote);
public sealed record CreateIncidentRequest(string Type, string Details);
public sealed record CreateAuditRequest(string Action, string Details);
