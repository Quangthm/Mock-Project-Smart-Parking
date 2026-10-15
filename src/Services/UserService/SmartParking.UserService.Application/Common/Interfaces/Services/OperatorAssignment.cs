namespace UserService.Application.Common.Interfaces.Services;

public sealed record OperatorAssignment(Guid SiteId, Guid TenantId, string[] Permissions);
