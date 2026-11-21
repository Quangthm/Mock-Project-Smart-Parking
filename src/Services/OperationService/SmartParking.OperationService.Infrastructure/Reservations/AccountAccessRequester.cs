using Microsoft.Extensions.DependencyInjection.Extensions;
using MassTransit;
using SmartParking.Contracts.Audit;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartParking.Contracts.AccountAccess;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Infrastructure.Messaging;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class AccountAccessRequester(ISendEndpointProvider endpoints) : IAccountAccessRequester
{
    public async Task RequestAsync(AccountAccessRequestV1 request, CancellationToken ct)
    {
        var endpoint = await endpoints.GetSendEndpoint(new Uri($"queue:{AccountAccessEndpoints.Requests}"));
        await endpoint.Send(request, context =>
        {
            context.MessageId = request.AttemptId;
            context.CorrelationId = request.WorkflowId;
        }, ct);
    }
}
