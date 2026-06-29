namespace E6CarSpa.Contracts;

public record VehicleDto(Guid Id, string CarNumber, string CarModel);

public record CustomerDto(Guid Id, string Name, string Phone, List<VehicleDto> Vehicles, DateTime CreatedAt);

/// <summary>Result of looking a customer up by phone or car number during intake.</summary>
public record CustomerLookupResult(bool Found, CustomerDto? Customer);

/// <summary>Step 1 of the workflow: capture customer + car when the vehicle arrives.</summary>
public record IntakeRequest(string Name, string Phone, string CarNumber, string CarModel);
