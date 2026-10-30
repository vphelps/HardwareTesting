using Microsoft.Data.SqlClient;

namespace HardwareTesting.StressTest;

/// <summary>Snapshots sys.dm_os_wait_stats so a before/after run can be diffed into deltas.</summary>
public static class SqlWaitStatsCollector
{
    public static async Task<Dictionary<string, (long WaitMs, long WaitingTasks)>> SnapshotAsync(
        string connectionString, CancellationToken ct = default)
    {
        const string sql = """
            SELECT wait_type, wait_time_ms, waiting_tasks_count
            FROM sys.dm_os_wait_stats
            WHERE wait_time_ms > 0
              AND wait_type NOT LIKE 'SLEEP%'
              AND wait_type NOT IN ('BROKER_TASK_STOP', 'BROKER_TO_FLUSH', 'CLR_AUTO_EVENT',
                                     'LAZYWRITER_SLEEP', 'WAITFOR', 'XE_TIMER_EVENT');
            """;

        var result = new Dictionary<string, (long, long)>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result[reader.GetString(0)] = (reader.GetInt64(1), reader.GetInt64(2));
        }
        return result;
    }

    public static List<WaitStatDelta> Diff(
        Dictionary<string, (long WaitMs, long WaitingTasks)> before,
        Dictionary<string, (long WaitMs, long WaitingTasks)> after)
    {
        var deltas = new List<WaitStatDelta>();
        foreach (var (waitType, afterValue) in after)
        {
            before.TryGetValue(waitType, out var beforeValue);
            long waitDelta = afterValue.WaitMs - beforeValue.WaitMs;
            long tasksDelta = afterValue.WaitingTasks - beforeValue.WaitingTasks;
            if (waitDelta > 0)
            {
                deltas.Add(new WaitStatDelta(waitType, waitDelta, tasksDelta));
            }
        }
        return [.. deltas.OrderByDescending(d => d.WaitTimeMsDelta)];
    }
}
