using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.FacilityService.API;
using SmartParking.FacilityService.Application.Reservations;
using SmartParking.FacilityService.Infrastructure;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Infrastructure.Reservations;

namespace SmartParking.FacilityService.Tests;

public sealed class ReservationSlotEndpointTests
{
    [Fact]
    public async Task SignedSlotOnlyReadDerivesScopeAndDistinguishesMissingSlot()
    {
        using var rsa = RSA.Create(2048);
        var slot = new SelectedSlotMetadata(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await using var host = FacilityHost.Build([], builder =>
        {
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection();
            builder.Configuration["Services:Key"] = "reservation-slot-endpoint-test-service-key";
            builder.Configuration["ConnectionStrings:Parking"] = "Host=localhost;Port=1;Database=unused";
            builder.Configuration["ReservationIntegration:Enabled"] = "true";
            builder.Configuration["ReservationIntegration:OperationsPublicKeyPem"] = rsa.ExportRSAPublicKeyPem();
            builder.WebHost.UseTestServer();
            builder.Services.AddControllers().AddApplicationPart(typeof(FacilityHost).Assembly);
            builder.Host.ConfigureContainer<IServiceCollection>((_, services) =>
            {
                services.AddSingleton<IReservationSlotReader>(new EmptyReservationDependencies { Slot = slot });
                var worker = services.FirstOrDefault(d => d.ImplementationType == typeof(StructureSynchronizationWorker));
                if (worker is not null) services.Remove(worker);
            });
        });
        await host.StartAsync();
        using var http = host.GetTestClient();
        http.BaseAddress = new("https://localhost");
        using var anonymous = await http.GetAsync($"/internal/reservation-dependencies/v1/slots/{slot.SlotId}");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReservationIntegration:Enabled"] = "true",
            ["ReservationIntegration:OperationsPrivateKeyPem"] = rsa.ExportRSAPrivateKeyPem()
        }).Build();
        var client = new ReservationFacilityClient(http, configuration);
        var result = await client.ResolveSlotAsync(slot.SlotId, default);
        Assert.Equal((slot.SlotId, slot.SiteId, slot.TenantId), (result.SlotId, result.SiteId, result.TenantId));
        var error = await Assert.ThrowsAsync<ReservationGateException>(() => client.ResolveSlotAsync(Guid.NewGuid(), default));
        Assert.Equal((404, "SLOT_NOT_FOUND"), (error.StatusCode, error.Code));
    }
}
