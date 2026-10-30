namespace HardwareTesting.StressTest;

/// <summary>Static facts about the machine under test, captured once per run.</summary>
public sealed record HardwareInventory(
    string MachineName,
    string OsVersion,
    string CpuModel,
    int PhysicalCores,
    int LogicalCores,
    double RamTotalGb,
    int RamSpeedMhz,
    IReadOnlyList<DiskInfo> Disks,
    DateTimeOffset CollectedAtUtc);

public sealed record DiskInfo(string Model, string MediaType, double SizeGb);

/// <summary>One sampled point of OS-level resource usage, taken periodically while a workload runs.</summary>
public sealed record ResourceSample(
    DateTimeOffset TimestampUtc,
    double CpuPercent,
    double AvailableRamGb,
    double DiskPercentIdle,
    double DiskQueueLength);

/// <summary>Timing result for a single executed query.</summary>
public sealed record QueryResult(
    string QueryName,
    int Iteration,
    int WorkerId,
    long DurationMs,
    int RowsAffected,
    string? Error);

/// <summary>Delta of SQL Server wait stats between the start and end of a run.</summary>
public sealed record WaitStatDelta(string WaitType, long WaitTimeMsDelta, long WaitingTasksDelta);
