using SmartParking.UserService.Domain.Entities;

namespace UserService.Application.Common.Interfaces.Persistence;

public sealed record VehicleBinding(Guid VehicleId, Guid AccountId, string VehicleType, long Revision, string CanonicalPlate = "");
