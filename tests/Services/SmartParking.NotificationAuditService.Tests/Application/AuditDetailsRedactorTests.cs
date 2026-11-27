using System.Text.Json;
using SmartParking.NotificationAuditService.Application.Common.Security;

namespace SmartParking.NotificationAuditService.Tests.Application;

public class AuditDetailsRedactorTests
{
    [Fact]
    public void Redact_ShouldMaskSensitiveKeysRecursivelyWhilePreservingSafeFields()
    {
        const string raw = """
        {
          "reason": "Account locked by admin",
          "password": "SuperSecretPassword123!",
          "nested": {
            "access_token": "jwt-token-value",
            "otpCode": "123456",
            "siteId": "11111111-1111-1111-1111-111111111111"
          },
          "items": [
            { "refreshToken": "refresh-xyz", "status": "ACTIVE" }
          ]
        }
        """;

        var redacted = AuditDetailsRedactor.Redact(raw);
        using var doc = JsonDocument.Parse(redacted);
        var root = doc.RootElement;

        Assert.Equal("Account locked by admin", root.GetProperty("reason").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty("password").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty("nested").GetProperty("access_token").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty("nested").GetProperty("otpCode").GetString());
        Assert.Equal("11111111-1111-1111-1111-111111111111", root.GetProperty("nested").GetProperty("siteId").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty("items")[0].GetProperty("refreshToken").GetString());
        Assert.Equal("ACTIVE", root.GetProperty("items")[0].GetProperty("status").GetString());
    }

    [Fact]
    public void Redact_WhenJsonIsMalformed_ShouldFailClosedWithoutThrowing()
    {
        var redacted = AuditDetailsRedactor.Redact("not-valid-json{{{");
        Assert.Contains("UNPARSEABLE_REDACTED", redacted);
    }
}
