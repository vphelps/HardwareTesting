using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace HardwareTesting.StressTest;

/// <summary>A named query loaded from a .sql file, used as one unit of the simulated workload.</summary>
public sealed record NamedQuery(string Name, string CommandText);

public static class QueryLibrary
{
    /// <summary>Loads every *.sql file in a folder as a named query (filename, minus extension, is the name).</summary>
    public static List<NamedQuery> LoadFromFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            return [];
        }

        return [.. Directory.EnumerateFiles(folderPath, "*.sql")
            .OrderBy(f => f)
            .Select(f => new NamedQuery(Path.GetFileNameWithoutExtension(f), File.ReadAllText(f)))];
    }
}

/// <summary>
/// Runs a query mix against SQL Server with a configurable number of concurrent workers
/// (simulated workstations), recording per-execution timing for the CSV report.
/// </summary>
public sealed class QueryRunner(string connectionString, IReadOnlyList<NamedQuery> queries)
{
    public async Task<List<QueryResult>> RunAsync(
        int workerCount, int iterationsPerWorker, CancellationToken ct = default)
    {
        if (queries.Count == 0)
        {
            throw new InvalidOperationException("No queries loaded — nothing to run.");
        }

        var results = new System.Collections.Concurrent.ConcurrentBag<QueryResult>();

        var workers = Enumerable.Range(0, workerCount).Select(workerId =>
            Task.Run(async () =>
            {
                var rng = new Random(workerId);
                for (int i = 0; i < iterationsPerWorker && !ct.IsCancellationRequested; i++)
                {
                    var query = queries[rng.Next(queries.Count)];
                    results.Add(await ExecuteOnceAsync(query, workerId, i, ct));
                }
            }, ct));

        await Task.WhenAll(workers);
        return [.. results.OrderBy(r => r.WorkerId).ThenBy(r => r.Iteration)];
    }

    private async Task<QueryResult> ExecuteOnceAsync(NamedQuery query, int workerId, int iteration, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(query.CommandText, conn) { CommandTimeout = 120 };
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            int rows = 0;
            while (await reader.ReadAsync(ct))
            {
                rows++;
            }
            sw.Stop();
            return new QueryResult(query.Name, iteration, workerId, sw.ElapsedMilliseconds, rows, Error: null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new QueryResult(query.Name, iteration, workerId, sw.ElapsedMilliseconds, RowsAffected: -1, Error: ex.Message);
        }
    }
}
