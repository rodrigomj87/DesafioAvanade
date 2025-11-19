using System;

namespace ApiGateway.Configurations;

internal sealed class RateLimitingSettings
{
    public static RateLimitingSettings Default => new() { PermitLimit = 30, WindowSeconds = 10, QueueLimit = 0 };

    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }
    public int QueueLimit { get; set; }

    public static RateLimitingSettings Normalize(RateLimitingSettings? source)
    {
        var normalized = source is null
            ? new RateLimitingSettings()
            : new RateLimitingSettings
            {
                PermitLimit = source.PermitLimit,
                WindowSeconds = source.WindowSeconds,
                QueueLimit = source.QueueLimit
            };

        if (normalized.PermitLimit <= 0)
        {
            normalized.PermitLimit = Default.PermitLimit;
        }

        if (normalized.WindowSeconds <= 0)
        {
            normalized.WindowSeconds = Default.WindowSeconds;
        }

        if (normalized.QueueLimit < 0)
        {
            normalized.QueueLimit = Default.QueueLimit;
        }

        return normalized;
    }
}
