using System.Reflection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Authorization;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;

namespace SmartParking.UserService.Tests;

public sealed class PublicationEvidenceTests
{
    [Fact]
    public async Task ActualMediatorRequiresFinalApprovalAndExactOwnedInitialLot()
    {
        var actor=Guid.NewGuid(); var site=Guid.NewGuid();
        var store=DispatchProxy.Create<IIdentityWorkflowStore, Store>();
        var fake=(Store)store;
        fake.Decision = new OwnerApplication { Id=site, UserId=actor, Status="pending", LotType="multi-storey",
            LotSetupJson="{}", ReviewedAt=DateTimeOffset.UtcNow, ReviewedBy=Guid.NewGuid() };
        var auth=DispatchProxy.Create<IAccountAuthorizationService, Authorization>();
        ((Authorization)auth).Scope = new(actor, [site]);
        var services=new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<PublicationEvidenceHandler>());
        services.AddSingleton(store); services.AddSingleton(auth);
        await using var provider=services.BuildServiceProvider();
        var mediator=provider.GetRequiredService<IMediator>();
        Assert.Equal("RESOURCE_NOT_FOUND",(await Assert.ThrowsAsync<AuthException>(() => mediator.Send(new PublicationEvidenceQuery(site)))).Code);
        fake.Decision.Status="approved";
        Assert.Equal("multi_storey",(await mediator.Send(new PublicationEvidenceQuery(site))).LotType);
        await Assert.ThrowsAsync<AuthException>(() => mediator.Send(new PublicationEvidenceQuery(Guid.NewGuid())));
        fake.Decision.UserId=Guid.NewGuid();
        await Assert.ThrowsAsync<AuthException>(() => mediator.Send(new PublicationEvidenceQuery(site)));
        fake.Decision.UserId=actor; fake.Decision.ReviewedAt=null;
        await Assert.ThrowsAsync<AuthException>(() => mediator.Send(new PublicationEvidenceQuery(site)));
    }
    public class Store : DispatchProxy
    {
        public OwnerApplication Decision = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "FindOwnerApplicationByUserAsync" => Task.FromResult<OwnerApplication?>(Decision),
            _ => throw new NotSupportedException(method.Name)
        };
    }
    public class Authorization : DispatchProxy
    {
        public IdentityScope Scope=null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "OwnerScopeAsync" => Task.FromResult(Scope),
            _ => throw new NotSupportedException(method.Name)
        };
    }
}
