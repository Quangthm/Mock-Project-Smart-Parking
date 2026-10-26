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
            // UserService publishes only; no consumer/inbox cleanup worker is needed.
            outbox.DisableInboxCleanupService();
            outbox.UseBusOutbox(busOutbox =>
            {
                if (!configuration.GetValue<bool>("Messaging:OutboxDeliveryEnabled"))
                    busOutbox.DisableDeliveryService();
            });
        });
    }
}
