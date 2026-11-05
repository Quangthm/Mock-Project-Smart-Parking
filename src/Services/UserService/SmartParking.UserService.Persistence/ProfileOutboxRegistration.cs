using MassTransit;
using Microsoft.Extensions.Configuration;

namespace UserService.Persistence;

public static class ProfileOutboxRegistration
{
    public static void AddProfileOutbox(this IBusRegistrationConfigurator registration, IConfiguration configuration)
    {
        registration.AddEntityFrameworkOutbox<AppDbContext>(outbox =>
        {
            outbox.UsePostgres();
            outbox.QueryDelay = TimeSpan.FromSeconds(1);
            outbox.UseBusOutbox(busOutbox =>
            {
                if (!bool.TryParse(configuration["Messaging:OutboxDeliveryEnabled"], out var enabled) || !enabled)
                    busOutbox.DisableDeliveryService();
            });
        });
    }
}
