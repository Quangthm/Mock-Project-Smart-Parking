using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using SmartParking.ReservationService.Application.Common.Interfaces.Persistence;

namespace SmartParking.ReservationService.Persistence;

public static class PersistenceDependencyInjection
{
    public static IServiceCollection AddPersistenceServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Reservation")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:Reservation.");

        services.AddDbContext<ReservationDbContext>(options => options.UseNpgsql(connectionString));
        services.TryAddScoped<IStructureSafetyStore, StructureSafetyStore>();
        services.AddScoped<IReservationGateStore, ReservationGateStore>();
        return services;
    }
}
