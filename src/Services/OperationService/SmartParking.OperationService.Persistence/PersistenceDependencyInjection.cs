using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Persistence.Repositories;

namespace SmartParking.OperationService.Persistence;

public static class PersistenceDependencyInjection
{
    public static IServiceCollection AddPersistenceServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Reservation")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:Reservation.");

        services.AddDbContext<ReservationDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<ReservationRepository>();
        services.AddScoped<IReservationRepository>(provider => provider.GetRequiredService<ReservationRepository>());
        services.AddScoped<Application.Reservations.IReservationBookingRepository>(provider => provider.GetRequiredService<ReservationRepository>());
        services.AddScoped<Application.Reservations.IReservationTermsRepository, ReservationTermsRepository>();
        services.AddScoped<Application.Reservations.IReservationFacilityDependencyRepository, ReservationFacilityDependencyRepository>();
        services.AddScoped<IParkingSessionRepository, ParkingSessionRepository>();
        services.AddScoped<ISlotAllocationRepository, SlotAllocationRepository>();
        services.TryAddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<Application.Reservations.IReservationRequestRepository, ReservationRequestRepository>();
        services.AddScoped<Application.Reservations.IReservationWorkflowConcurrency, ReservationWorkflowConcurrency>();
        services.AddScoped<Application.Reservations.IReservationPolicyRepository, ReservationPolicyRepository>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IStructureRepository, StructureRepository>();
        services.AddScoped<OperationsConcurrency>();
        services.TryAddScoped<IOperationsConcurrency>(provider => provider.GetRequiredService<OperationsConcurrency>());
        return services;
    }
}
