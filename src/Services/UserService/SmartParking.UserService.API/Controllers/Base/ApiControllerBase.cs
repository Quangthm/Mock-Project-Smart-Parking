using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace UserService.API.Controllers.Base
{
    /// <summary>
    /// Base API Controller.
    /// </summary>
    /// <seealso cref="Microsoft.AspNetCore.Mvc.ControllerBase" />
    /// <remarks>
    /// Initializes a new instance of the <see cref="ApiControllerBase"/> class.
    /// </remarks>
    /// <param name="mediator">The mediator.</param>
    /// <param name="mapper">The mapper.</param>
    [ApiController]
    public abstract class ApiControllerBase(IMediator mediator, IMapper mapper) : ControllerBase
    {
        /// <summary>
        /// The mediator
        /// </summary>
        public readonly IMediator Mediator = mediator;

        /// <summary>
        /// The mapper
        /// </summary>
        public readonly IMapper Mapper = mapper;
    }
}
