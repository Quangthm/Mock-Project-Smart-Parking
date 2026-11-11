using UserService.Domain.Base;

namespace SmartParking.UserService.Domain.Entities;

public sealed class UserSimulatedIdentity : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>
    /// Substitute GUID returned from the simulated VNeID information-sharing process.
    /// Not a real government ID, national identification number, or authentication credential.
    /// </summary>
    public Guid SimulatedVNeIdGuid { get; set; }

    public bool ConsentGiven { get; set; }
    public DateTimeOffset ConsentTimestamp { get; set; }
    public string ConsentVersion { get; set; } = string.Empty;

    /// <summary>
    /// Simulation status: COMPLETED, FAILED, or DECLINED.
    /// </summary>
    public string SimulationStatus { get; set; } = "COMPLETED";

    /// <summary>
    /// Masked/mock identity representation for display purposes only (e.g. 001090******).
    /// </summary>
    public string? MockDocumentNumber { get; set; }

    /// <summary>
    /// Mock full name returned during simulation.
    /// </summary>
    public string? MockFullName { get; set; }
}

