using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using SmartParking.OperationService.Application.Discovery;

namespace SmartParking.OperationService.Infrastructure.Clients;

public sealed class DiscoveryOrigins(Uri? facility, Uri? payment, string? paymentKey)
{
    public Uri? Facility { get; } = facility;
    public Uri? Payment { get; } = payment;
    public string? PaymentKey { get; } = paymentKey;
}
