using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.FacilityService.Application.Common.Synchronization;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Reservations;
using SmartParking.FacilityService.Domain.Entities;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.FacilityService.Tests;

internal static class StructureTestMediator
{
    public static IMediator Create(StructureSynchronizationHandlers handler)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(StructureTestMediator).Assembly));
        services.AddSingleton<IRequestHandler<RecoverStructureSiteCommand>>(handler);
        services.AddSingleton<IRequestHandler<PublishStructureOutcomeCommand>>(handler);
        services.AddSingleton<IRequestHandler<RecoverPendingStructuresCommand, IReadOnlyList<(Guid SiteId, Exception Error)>>>(handler);
        return new Mediator(services.BuildServiceProvider());
    }
}
