using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SmartParking.OperationService.API.Controllers.Base;

[ApiController]
public abstract class ApiControllerBase(IMediator mediator) : ControllerBase
{
    protected IMediator Mediator { get; } = mediator;
}
