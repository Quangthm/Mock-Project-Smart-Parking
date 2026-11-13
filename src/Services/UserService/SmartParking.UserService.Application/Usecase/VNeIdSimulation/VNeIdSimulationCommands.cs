using FluentValidation;
using MediatR;
using UserService.Application.DTOs;
using UserService.Application.Common.Interfaces.Services;

namespace UserService.Application.Usecase.VNeIdSimulation;

public sealed record SimulateDriverVNeIdCommand(SimulatedVNeIdRequest Body) : IRequest<SimulatedVNeIdResult>;

public sealed class SimulateDriverVNeIdValidator : AbstractValidator<SimulateDriverVNeIdCommand>
{
    public SimulateDriverVNeIdValidator()
    {
        RuleFor(x => x.Body).NotNull();
        When(x => x.Body is not null, () =>
        {
            RuleFor(x => x.Body.RegistrationId).NotEmpty().WithMessage("Registration ID is required.");
            RuleFor(x => x.Body.ConsentConfirmed).Equal(true).WithMessage("Explicit consent for simulated VNeID information-sharing is required.");
        });
    }
}

public sealed class SimulateDriverVNeIdCommandHandler(IVNeIdSimulationService vneid)
    : IRequestHandler<SimulateDriverVNeIdCommand, SimulatedVNeIdResult>
{
    public Task<SimulatedVNeIdResult> Handle(SimulateDriverVNeIdCommand request, CancellationToken cancellationToken) =>
        vneid.CompleteDriverSimulationAsync(request.Body, cancellationToken);
}

public sealed record SimulateOwnerVNeIdCommand(SimulatedVNeIdRequest Body) : IRequest<SimulatedVNeIdResult>;

public sealed class SimulateOwnerVNeIdValidator : AbstractValidator<SimulateOwnerVNeIdCommand>
{
    public SimulateOwnerVNeIdValidator()
    {
        RuleFor(x => x.Body).NotNull();
        When(x => x.Body is not null, () =>
        {
            RuleFor(x => x.Body.RegistrationId).NotEmpty().WithMessage("Registration / Application ID is required.");
            RuleFor(x => x.Body.ConsentConfirmed).Equal(true).WithMessage("Explicit consent for simulated VNeID information-sharing is required.");
        });
    }
}

public sealed class SimulateOwnerVNeIdCommandHandler(IVNeIdSimulationService vneid)
    : IRequestHandler<SimulateOwnerVNeIdCommand, SimulatedVNeIdResult>
{
    public Task<SimulatedVNeIdResult> Handle(SimulateOwnerVNeIdCommand request, CancellationToken cancellationToken) =>
        vneid.CompleteOwnerSimulationAsync(request.Body, cancellationToken);
}

public sealed record SimulateOperatorVNeIdCommand(SimulatedVNeIdRequest Body) : IRequest<SimulatedVNeIdResult>;

public sealed class SimulateOperatorVNeIdValidator : AbstractValidator<SimulateOperatorVNeIdCommand>
{
    public SimulateOperatorVNeIdValidator()
    {
        RuleFor(x => x.Body).NotNull();
        When(x => x.Body is not null, () =>
        {
            RuleFor(x => x.Body.RegistrationId).NotEmpty().WithMessage("Challenge ID is required.");
            RuleFor(x => x.Body.ConsentConfirmed).Equal(true).WithMessage("Explicit consent for simulated VNeID information-sharing is required.");
        });
    }
}

public sealed class SimulateOperatorVNeIdCommandHandler(IVNeIdSimulationService vneid)
    : IRequestHandler<SimulateOperatorVNeIdCommand, SimulatedVNeIdResult>
{
    public Task<SimulatedVNeIdResult> Handle(SimulateOperatorVNeIdCommand request, CancellationToken cancellationToken) =>
        vneid.CompleteOperatorSimulationAsync(request.Body.RegistrationId, request.Body.ConsentConfirmed, request.Body.ConsentVersion, cancellationToken);
}

public sealed record GetVNeIdStatusQuery(Guid UserId) : IRequest<VNeIdSimulationStatusDto>;

public sealed class GetVNeIdStatusQueryHandler(IVNeIdSimulationService vneid)
    : IRequestHandler<GetVNeIdStatusQuery, VNeIdSimulationStatusDto>
{
    public Task<VNeIdSimulationStatusDto> Handle(GetVNeIdStatusQuery request, CancellationToken cancellationToken) =>
        vneid.GetStatusByUserIdAsync(request.UserId, cancellationToken);
}
