using System.Globalization;

namespace ContainerAutoShutdown.Helpers;

public static class DurationParser
{
    /// <summary>
    /// Parses a human-friendly duration string into a <see cref="TimeSpan"/>.
    /// Supported suffixes: s (seconds), m (minutes), h (hours), d (days).
    /// Examples: "30s", "5m", "2h", "1.5h", "1d".
    /// </summary>
    public static bool TryParse(string? input, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim().ToLowerInvariant();

        if (trimmed.Length < 2)
            return false;

        var suffix = trimmed[^1];

        if (!double.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value <= 0)
            return false;

        duration = suffix switch
        {
            's' => TimeSpan.FromSeconds(value),
            'm' => TimeSpan.FromMinutes(value),
            'h' => TimeSpan.FromHours(value),
            'd' => TimeSpan.FromDays(value),
            _ => TimeSpan.Zero
        };

        return duration > TimeSpan.Zero;
    }
}
