using MediatR;
using SmartParking.PaymentService.Application.DTOs.PassPlans;

namespace SmartParking.PaymentService.Application.Usecase.PassPlans;

public sealed record CompensatePassClosureCommand(CompensatePassClosureRequest Request)
    : IRequest<CompensatePassClosureResponseDto>;
