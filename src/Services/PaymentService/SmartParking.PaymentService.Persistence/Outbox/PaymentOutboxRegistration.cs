using MassTransit;
using Microsoft.Extensions.Configuration;

namespace SmartParking.PaymentService.Persistence.Outbox;

public static class PaymentOutboxRegistration
{
    public static void AddPaymentOutbox(this IBusRegistrationConfigurator registration, IConfiguration configuration)
    {
        registration.AddEntityFrameworkOutbox<PaymentDbContext>(outbox =>
        {
            outbox.UsePostgres();
            outbox.QueryDelay = TimeSpan.FromSeconds(1);
            outbox.DisableInboxCleanupService();
            outbox.UseBusOutbox();
        });
    }
}
