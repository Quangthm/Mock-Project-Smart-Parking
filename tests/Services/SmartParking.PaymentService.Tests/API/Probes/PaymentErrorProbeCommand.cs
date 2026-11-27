using MediatR;

namespace SmartParking.PaymentService.Tests.API.Probes;

public sealed record PaymentErrorProbeCommand(string? Name) : IRequest<string>;
