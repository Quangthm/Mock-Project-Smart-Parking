using System.Text.Json.Serialization;
namespace SmartParking.PaymentService.Application.Reservations;

public sealed record QuoteCoverage([property: JsonRequired] bool IsCovered, string Version);
