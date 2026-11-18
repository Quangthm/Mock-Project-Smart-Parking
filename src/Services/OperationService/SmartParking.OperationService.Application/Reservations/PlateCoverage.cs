using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record PlateCoverage([property: JsonRequired] bool IsCovered, string Version);
