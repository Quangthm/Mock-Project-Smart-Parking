using System.Security.Cryptography;

namespace SmartParking.NotificationAuditService.Infrastructure.Security;

public sealed record JwksRsaKey(string Kid, RSAParameters Parameters);
