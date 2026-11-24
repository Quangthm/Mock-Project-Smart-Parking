using MassTransit;
using SmartParking.Contracts.Facility;

namespace SmartParking.FacilityService.Tests.TestDoubles;

internal sealed class RecordingPublishEndpoint : IPublishEndpoint
{
    public List<LayoutInvalidatedV1> Published { get; } = [];
    public List<FacilityCapacityRestrictedV1> Restricted { get; } = [];
    public List<FacilityCapacityRestrictionReleasedV1> Released { get; } = [];

    public ConnectHandle ConnectPublishObserver(IPublishObserver observer) => throw new NotSupportedException();

    public Task Publish<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        if (message is LayoutInvalidatedV1 evt)
            Published.Add(evt);
        if (message is FacilityCapacityRestrictedV1 restricted)
            Restricted.Add(restricted);
        if (message is FacilityCapacityRestrictionReleasedV1 released)
            Released.Add(released);
        return Task.CompletedTask;
    }

    public Task Publish<T>(T message, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default) where T : class =>
        throw new NotSupportedException();

    public Task Publish<T>(T message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) where T : class =>
        throw new NotSupportedException();

    public Task Publish(object message, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task Publish(object message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task Publish(object message, Type messageType, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task Publish(object message, Type messageType, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task Publish<T>(object values, CancellationToken cancellationToken = default) where T : class =>
        throw new NotSupportedException();

    public Task Publish<T>(object values, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default) where T : class =>
        throw new NotSupportedException();

    public Task Publish<T>(object values, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) where T : class =>
        throw new NotSupportedException();
}
