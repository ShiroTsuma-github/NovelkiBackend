namespace Infrastructure.Services;

public sealed class BookImportSecurityOptions
{
    public const string SectionName = "BookImports:Security";

    public int MaxArchiveEntries { get; set; } = 5_000;
    public int MaxCsvRows { get; set; } = 50_000;
    public int MaxManifestBooks { get; set; } = 50_000;
    public long MaxCsvBytes { get; set; } = 10 * 1024 * 1024;
    public long MaxManifestBytes { get; set; } = 2 * 1024 * 1024;
    public long MaxCoverBytes { get; set; } = 10 * 1024 * 1024;
    public long MaxUncompressedArchiveBytes { get; set; } = 256 * 1024 * 1024;
    public double MaxCompressionRatio { get; set; } = 100;
    public double SuspiciousCompressionRatio { get; set; } = 500;
    public long SuspiciousCompressionMinimumBytes { get; set; } = 8 * 1024 * 1024;
    public TimeSpan SuspiciousAccountBlockDuration { get; set; } = TimeSpan.FromHours(24);
    public int MaxConcurrentFullImportOperations { get; set; } = 2;
    public int MaxActiveSessionsGlobal { get; set; } = 50;
    public int MaxActiveSessionsPerUser { get; set; } = 5;
    public int MaxActiveFullSessionsGlobal { get; set; } = 10;
    public int MaxActiveFullSessionsPerUser { get; set; } = 1;
    public long MaxStagedBytesGlobal { get; set; } = 1024L * 1024 * 1024;
    public TimeSpan SessionIdleTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan SessionAbsoluteLifetime { get; set; } = TimeSpan.FromHours(2);
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan DraftProcessingTimeout { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan FinalizeProcessingTimeout { get; set; } = TimeSpan.FromMinutes(5);
}

public sealed class FullImportCapacityExceededException : Exception
{
    public FullImportCapacityExceededException(string message) : base(message)
    {
    }
}

public sealed class ImportCapacityExceededException : Exception
{
    public ImportCapacityExceededException(string message) : base(message)
    {
    }
}

public sealed class BookImportProcessingTimeoutException : Exception
{
    public BookImportProcessingTimeoutException(string message) : base(message)
    {
    }
}

public sealed class AccountTemporarilyBlockedException : Exception
{
    public AccountTemporarilyBlockedException(DateTimeOffset blockedUntilUtc)
        : base(
            $"This account is blocked until {blockedUntilUtc.UtcDateTime:yyyy-MM-dd HH:mm:ss} UTC because suspicious activity was detected.")
    {
        BlockedUntilUtc = blockedUntilUtc;
    }

    public DateTimeOffset BlockedUntilUtc { get; }
}

internal sealed class SuspiciousBookImportException : Exception
{
    public SuspiciousBookImportException(string reasonCode, string safeMessage) : base(safeMessage)
    {
        ReasonCode = reasonCode;
    }

    public string ReasonCode { get; }
}
