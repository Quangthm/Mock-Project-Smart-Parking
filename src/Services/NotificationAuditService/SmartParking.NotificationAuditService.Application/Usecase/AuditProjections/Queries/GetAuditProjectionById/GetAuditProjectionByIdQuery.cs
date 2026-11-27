using MediatR;
using SmartParking.NotificationAuditService.Application.DTOs.Audit;

namespace SmartParking.NotificationAuditService.Application.Usecase.AuditProjections.Queries.GetAuditProjectionById;

public sealed record GetAuditProjectionByIdQuery(Guid Id) : IRequest<AuditProjectionDto>;
