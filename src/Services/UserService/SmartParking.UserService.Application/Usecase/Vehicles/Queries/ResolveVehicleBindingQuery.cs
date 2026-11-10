using FluentValidation;
using MediatR;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Models.Exceptions;

namespace UserService.Application.Usecase.Vehicles;

public sealed record ResolveVehicleBindingQuery(Guid AccountId, Guid VehicleId, long? ExpectedRevision) : IRequest<BindingResponse>;

public sealed class ResolveVehicleBindingValidator : AbstractValidator<ResolveVehicleBindingQuery>
{
    public ResolveVehicleBindingValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.ExpectedRevision).Must(revision => revision is null or > 0);
    }
}

public sealed class ResolveVehicleBindingQueryHandler(IVehicleRepository vehicles)
    : IRequestHandler<ResolveVehicleBindingQuery, BindingResponse>
{
    public async Task<BindingResponse> Handle(ResolveVehicleBindingQuery request, CancellationToken ct)
    {
        var binding = await vehicles.ResolveBindingAsync(request.AccountId, request.VehicleId, ct);
        if (binding is null) return new("INELIGIBLE");
        if (request.ExpectedRevision is { } revision && binding.Revision != revision)
            throw new AuthException("VEHICLE_BINDING_STALE", "Vehicle binding changed; revalidate before continuing.", 409);
        return new("ACTIVE", binding.VehicleId, binding.AccountId, binding.VehicleType, binding.Revision);
    }
}
