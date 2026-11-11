namespace UserService.Application.DTOs;

public sealed record SimulatedVNeIdRequest(
    Guid RegistrationId,
    bool ConsentConfirmed,
    string? ConsentVersion = "v1.1-simulated"
);

public sealed record SimulatedVNeIdResult(
    Guid SimulatedVNeIdGuid,
    string SimulationStatus,
    string MockFullName,
    string MockDocumentNumber,
    DateTimeOffset CompletedAt
);

public sealed record VNeIdSimulationStatusDto(
    bool IsCompleted,
    Guid? SimulatedVNeIdGuid,
    string? MockFullName,
    string? MockDocumentNumber,
    DateTimeOffset? CompletedAt
);

