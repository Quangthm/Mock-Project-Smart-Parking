using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Infrastructure.Configurations;

namespace UserService.Infrastructure.Services;

public sealed class TurnstileVerifier(
    HttpClient httpClient,
    TurnstileOptions options,
    ILogger<TurnstileVerifier>? logger = null) : IBotVerificationService
{
    private static readonly Uri VerifyUri = new("https://challenges.cloudflare.com/turnstile/v0/siteverify");

    public async Task VerifyAsync(string? token, string action, CancellationToken ct, string? remoteIp = null)
    {
        if (!options.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new AuthException("BOT_VERIFICATION_REQUIRED", "Bot verification token is required.", 400);
        }

        if (token.Length > 2048)
        {
            throw new AuthException("BOT_VERIFICATION_FAILED", "Bot verification failed.", 400);
        }

        if (string.IsNullOrWhiteSpace(options.SecretKey))
        {
            logger?.LogError("Turnstile verification is enabled but SecretKey is not configured.");
            throw new AuthException("BOT_VERIFICATION_UNAVAILABLE", "Bot verification service is temporarily unavailable. Please retry.", 503);
        }

        var parameters = new List<KeyValuePair<string, string>>
        {
            new("secret", options.SecretKey),
            new("response", token)
        };

        if (!string.IsNullOrWhiteSpace(remoteIp))
        {
            parameters.Add(new("remoteip", remoteIp));
        }

        using var content = new FormUrlEncodedContent(parameters);

        try
        {
            using var response = await httpClient.PostAsync(VerifyUri, content, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger?.LogWarning("Turnstile provider returned HTTP {StatusCode}.", (int)response.StatusCode);
                throw new AuthException("BOT_VERIFICATION_UNAVAILABLE", "Bot verification provider communication error.", 503);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var result = await JsonSerializer.DeserializeAsync<TurnstileVerifyResponse>(stream, cancellationToken: ct);
            if (result is null)
            {
                throw new AuthException("BOT_VERIFICATION_UNAVAILABLE", "Malformed bot verification response.", 503);
            }

            if (!result.Success)
            {
                var errorCodes = result.ErrorCodes ?? [];
                if (errorCodes.Contains("timeout-or-duplicate", StringComparer.OrdinalIgnoreCase))
                {
                    logger?.LogWarning("Turnstile verification expired or already consumed for action {Action}.", action);
                    throw new AuthException("BOT_VERIFICATION_EXPIRED", "Bot verification token has expired or was already used. Please try again.", 400);
                }

                if (errorCodes.Contains("invalid-input-response", StringComparer.OrdinalIgnoreCase))
                {
                    logger?.LogWarning("Turnstile verification rejected invalid token for action {Action}.", action);
                    throw new AuthException("BOT_VERIFICATION_FAILED", "Bot verification failed.", 400);
                }

                logger?.LogWarning("Turnstile verification provider reported error codes: {ErrorCodes} for action {Action}.",
                    string.Join(',', errorCodes), action);
                throw new AuthException("BOT_VERIFICATION_UNAVAILABLE", "Bot verification service is temporarily unavailable. Please retry.", 503);
            }

            if (!string.Equals(result.Action, action, StringComparison.Ordinal))
            {
                logger?.LogWarning("Turnstile action mismatch: expected {ExpectedAction}, got {ReceivedAction}.", action, result.Action);
                throw new AuthException("BOT_VERIFICATION_FAILED", "Bot verification failed.", 400);
            }

            if (options.ExpectedHostnames is not { Length: > 0 } ||
                !options.ExpectedHostnames.Any(h => string.Equals(h, result.Hostname, StringComparison.OrdinalIgnoreCase)))
            {
                logger?.LogWarning("Turnstile hostname mismatch: received hostname {Hostname}.", result.Hostname);
                throw new AuthException("BOT_VERIFICATION_FAILED", "Bot verification failed.", 400);
            }

            logger?.LogInformation("Turnstile verification succeeded for action {Action}.", action);
        }
        catch (AuthException)
        {
            throw;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Client cancel hoặc app shutdown -> lan truyền nguyên vẹn, KHÔNG trả 503
            throw;
        }
        catch (OperationCanceledException)
        {
            // Provider timeout (5s) -> 503
            logger?.LogWarning("Turnstile verification timed out for action {Action}.", action);
            throw new AuthException("BOT_VERIFICATION_UNAVAILABLE", "Bot verification timed out. Please try again.", 503);
        }
        catch (HttpRequestException ex)
        {
            logger?.LogWarning(ex, "Cannot reach Turnstile provider for action {Action}.", action);
            throw new AuthException("BOT_VERIFICATION_UNAVAILABLE", "Cannot reach bot verification provider.", 503);
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex, "Malformed Turnstile response for action {Action}.", action);
            throw new AuthException("BOT_VERIFICATION_UNAVAILABLE", "Malformed bot verification response.", 503);
        }
        catch (IOException ex)
        {
            logger?.LogWarning(ex, "Turnstile communication interrupted for action {Action}.", action);
            throw new AuthException("BOT_VERIFICATION_UNAVAILABLE", "Communication interrupted during verification.", 503);
        }
    }
}
