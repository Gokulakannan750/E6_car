using E6CarSpa.Domain.Entities;

namespace E6CarSpa.Contracts;

/// <summary>Lightweight summary row for the showrooms list.</summary>
public record ShowroomDto(Guid Id, string Name, string Address, string? Phone, bool IsActive);

/// <summary>Request payload for creating a new showroom.</summary>
public record SaveShowroomRequest(string Name, string Address, string? Phone = null);

// ----- Visits -----

/// <summary>One visit returned to the client.</summary>
public record ShowroomVisitDto(Guid Id, Guid ShowroomId, string ShowroomName,
    DateTime VisitDate, string TeamSent, int VehiclesAttended, decimal Amount, string? Note);

/// <summary>Summary row for the visits list (grouped by showroom or date).</summary>
public record ShowroomVisitSummaryDto(Guid ShowroomId, string ShowroomName,
    int VisitCount, int TotalVehicles, decimal TotalAmount);

/// <summary>Request payload for creating a new visit.</summary>
public record SaveShowroomVisitRequest(Guid ShowroomId, DateTime VisitDate,
    string TeamSent, int VehiclesAttended, decimal Amount, string? Note = null);
