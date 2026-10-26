using System.Buffers;
using System.Text;

namespace SmartParking.UserService.Domain.ValueObjects;

/// <summary>
/// The display-name rules that apply to a person's own profile.
/// </summary>
/// <remarks>
/// The approved limit counts Unicode scalar values, matching PostgreSQL VARCHAR capacity.
/// Combining marks count separately; no Unicode normalization or inner-space folding occurs.
/// Names still require context-appropriate output encoding and must not be logged by default.
/// </remarks>
public static class ProfileName
{
    public const int MaxLength = 255;

    /// <summary>Whether the supplied value is an acceptable display name.</summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        // Reject controls even at the edges before trimming (including NUL, which PostgreSQL rejects).
        if (value.Any(char.IsControl)) return false;
        var remaining = value.AsSpan().Trim();
        var count = 0;
        while (!remaining.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(remaining, out _, out var consumed) != OperationStatus.Done)
                return false;
            if (++count > MaxLength) return false;
            remaining = remaining[consumed..];
        }
        return true;
    }

    /// <summary>The stored form of a display name.</summary>
    public static string Normalize(string value) => value.Trim();
}
