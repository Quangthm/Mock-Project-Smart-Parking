using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
namespace SmartParking.OperationService.Persistence.Repositories;
public sealed class AuditLogRepository(ReservationDbContext db) : GenericRepository<AuditLogRow>(db), IAuditLogRepository { }
