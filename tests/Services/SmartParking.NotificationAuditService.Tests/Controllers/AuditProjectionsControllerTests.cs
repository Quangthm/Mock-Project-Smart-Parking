using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartParking.NotificationAuditService.API.Controllers;
using SmartParking.NotificationAuditService.Application.DTOs.Audit;
using SmartParking.NotificationAuditService.Application.Usecase.AuditProjections.Queries.GetAuditProjectionById;
using SmartParking.NotificationAuditService.Application.Usecase.AuditProjections.Queries.SearchAuditProjections;

namespace SmartParking.NotificationAuditService.Tests.Controllers;

public class AuditProjectionsControllerTests
{
    private sealed class StubMediator : IMediator
    {
        public object? LastRequest { get; private set; }
        public Func<object, object?> Handler { get; set; } = _ => null;

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult((TResponse)Handler(request)!);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        {
            LastRequest = request;
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(Handler(request));
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }

    [Fact]
    public async Task Search_ShouldDispatchQueryAndReturnSuccessEnvelope()
    {
        var mediator = new StubMediator();
        var page = new AuditProjectionPageDto(
            Items: Array.Empty<AuditProjectionDto>(),
            TotalCount: 0,
            Page: 1,
            PageSize: 20,
            TotalPages: 0,
            ReadModel: new AuditReadModelMetadataDto(true, "7 years", "Async projection", DateTimeOffset.UtcNow));

        mediator.Handler = _ => page;

        var controller = new AuditProjectionsController(mediator);
        var result = await controller.Search(null, null, "USER_LOCKED", null, null, null, null, null, null, null, 1, 20);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        var sent = Assert.IsType<SearchAuditProjectionsQuery>(mediator.LastRequest);
        Assert.Equal("USER_LOCKED", sent.Action);
        Assert.Equal(1, sent.Page);
        Assert.Equal(20, sent.PageSize);
    }

    [Fact]
    public async Task GetById_ShouldDispatchQueryAndReturnOk()
    {
        var id = Guid.NewGuid();
        var mediator = new StubMediator();
        var dto = new AuditProjectionDto(
            id, "UserService", Guid.NewGuid(), Guid.NewGuid(), "Admin",
            "USER_LOCKED", "User", Guid.NewGuid(), "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 0);

        mediator.Handler = _ => dto;

        var controller = new AuditProjectionsController(mediator);
        var result = await controller.GetById(id);

        Assert.IsType<OkObjectResult>(result);
        var sent = Assert.IsType<GetAuditProjectionByIdQuery>(mediator.LastRequest);
        Assert.Equal(id, sent.Id);
    }
}
