using MassTransit;
using Microsoft.Extensions.Configuration;

namespace SmartParking.PaymentService.Persistence.Outbox;

public static class PaymentOutboxRegistration
{
    public static void AddPaymentOutbox(this IBusRegistrationConfigurator registration, IConfiguration configuration)
    {
        registration.AddConfigureEndpointsCallback((context, name, endpoint) =>
        {
            if (name is SmartParking.Contracts.ReservationPayments.ReservationPaymentEndpoints.EnsureOrder
                or SmartParking.Contracts.ReservationPayments.ReservationPaymentEndpoints.Disposition)
            {
                endpoint.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(1)));
                endpoint.UseEntityFrameworkOutbox<PaymentDbContext>(context);
            }
        });
        registration.AddEntityFrameworkOutbox<PaymentDbContext>(outbox =>
        {
            outbox.UsePostgres();
            outbox.QueryDelay = TimeSpan.FromSeconds(1);
            outbox.DisableInboxCleanupService();
            outbox.UseBusOutbox();
        });
    }
}
