using MediatR;

namespace SmartParking.NotificationAuditService.Tests.API.Probes;

public sealed record NotificationErrorProbeCommand(string? Name) : IRequest<string>;
