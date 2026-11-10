using System.Text.Json.Serialization;

namespace UserService.Infrastructure.Services;

public sealed class TurnstileVerifyResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("challenge_ts")] public string? ChallengeTs { get; set; }
    [JsonPropertyName("hostname")] public string? Hostname { get; set; }
    [JsonPropertyName("error-codes")] public string[]? ErrorCodes { get; set; }
    [JsonPropertyName("action")] public string? Action { get; set; }
    [JsonPropertyName("cdata")] public string? CData { get; set; }
}
