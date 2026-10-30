using System.Globalization;
using System.Text;

namespace HardwareTesting.StressTest;

/// <summary>Writes run results to CSV, one file per result type, under the given output folder.</summary>
public static class CsvReportWriter
{
    public static void WriteQueryResults(string outputFolder, IReadOnlyList<QueryResult> results)
    {
        var sb = new StringBuilder("QueryName,Iteration,WorkerId,DurationMs,RowsAffected,Error\n");
        foreach (var r in results)
        {
            sb.Append(CsvLine(r.QueryName, r.Iteration, r.WorkerId, r.DurationMs, r.RowsAffected, r.Error ?? ""));
        }
        File.WriteAllText(Path.Combine(outputFolder, "query-results.csv"), sb.ToString());
    }

    public static void WriteResourceSamples(string outputFolder, IReadOnlyList<ResourceSample> samples)
    {
        var sb = new StringBuilder("TimestampUtc,CpuPercent,AvailableRamGb,DiskPercentIdle,DiskQueueLength\n");
        foreach (var s in samples)
        {
            sb.Append(CsvLine(
                s.TimestampUtc.ToString("o", CultureInfo.InvariantCulture),
                s.CpuPercent, s.AvailableRamGb, s.DiskPercentIdle, s.DiskQueueLength));
        }
        File.WriteAllText(Path.Combine(outputFolder, "resource-samples.csv"), sb.ToString());
    }

    public static void WriteWaitStats(string outputFolder, IReadOnlyList<WaitStatDelta> deltas)
    {
        var sb = new StringBuilder("WaitType,WaitTimeMsDelta,WaitingTasksDelta\n");
        foreach (var d in deltas)
        {
            sb.Append(CsvLine(d.WaitType, d.WaitTimeMsDelta, d.WaitingTasksDelta));
        }
        File.WriteAllText(Path.Combine(outputFolder, "wait-stats.csv"), sb.ToString());
    }

    public static void WriteHardwareInventory(string outputFolder, HardwareInventory inv)
    {
        var sb = new StringBuilder("MachineName,OsVersion,CpuModel,PhysicalCores,LogicalCores,RamTotalGb,RamSpeedMhz,Disks,CollectedAtUtc\n");
        string disks = string.Join(" | ", inv.Disks.Select(d => $"{d.Model} ({d.MediaType}, {d.SizeGb:F0} GB)"));
        sb.Append(CsvLine(
            inv.MachineName, inv.OsVersion, inv.CpuModel, inv.PhysicalCores, inv.LogicalCores,
            inv.RamTotalGb, inv.RamSpeedMhz, disks, inv.CollectedAtUtc.ToString("o", CultureInfo.InvariantCulture)));
        File.WriteAllText(Path.Combine(outputFolder, "hardware-inventory.csv"), sb.ToString());
    }

    private static string CsvLine(params object[] fields) =>
        string.Join(",", fields.Select(Escape)) + "\n";

    private static string Escape(object field)
    {
        string s = Convert.ToString(field, CultureInfo.InvariantCulture) ?? "";
        return s.Contains(',') || s.Contains('"') || s.Contains('\n')
            ? $"\"{s.Replace("\"", "\"\"")}\""
            : s;
    }
}
