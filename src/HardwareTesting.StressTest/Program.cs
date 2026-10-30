using HardwareTesting.StressTest;

var options = RunOptions.Parse(args);
if (options is null)
{
    RunOptions.PrintUsage();
    return 1;
}

Directory.CreateDirectory(options.OutputFolder);

Console.WriteLine("Collecting hardware inventory...");
var inventory = HardwareInventoryCollector.Collect();
CsvReportWriter.WriteHardwareInventory(options.OutputFolder, inventory);
Console.WriteLine($"  {inventory.CpuModel}, {inventory.LogicalCores} logical cores, {inventory.RamTotalGb} GB RAM");

var queries = QueryLibrary.LoadFromFolder(options.QueriesFolder);
if (queries.Count == 0)
{
    Console.Error.WriteLine($"No .sql files found in '{options.QueriesFolder}'. Add at least one query to run.");
    return 1;
}
Console.WriteLine($"Loaded {queries.Count} quer{(queries.Count == 1 ? "y" : "ies")} from '{options.QueriesFolder}'.");

Console.WriteLine("Snapshotting SQL wait stats (before)...");
var waitsBefore = await SqlWaitStatsCollector.SnapshotAsync(options.ConnectionString);

using var sampler = new ResourceSampler(TimeSpan.FromSeconds(1));

Console.WriteLine($"Running workload: {options.Workers} worker(s) x {options.IterationsPerWorker} iteration(s)...");
var runner = new QueryRunner(options.ConnectionString, queries);
var results = await runner.RunAsync(options.Workers, options.IterationsPerWorker);

var resourceSamples = sampler.GetSamples();

Console.WriteLine("Snapshotting SQL wait stats (after)...");
var waitsAfter = await SqlWaitStatsCollector.SnapshotAsync(options.ConnectionString);
var waitDeltas = SqlWaitStatsCollector.Diff(waitsBefore, waitsAfter);

CsvReportWriter.WriteQueryResults(options.OutputFolder, results);
CsvReportWriter.WriteResourceSamples(options.OutputFolder, resourceSamples);
CsvReportWriter.WriteWaitStats(options.OutputFolder, waitDeltas);

var durations = results.Where(r => r.Error is null).Select(r => r.DurationMs).OrderBy(d => d).ToList();
if (durations.Count > 0)
{
    long p50 = durations[durations.Count / 2];
    long p95 = durations[(int)(durations.Count * 0.95)];
    Console.WriteLine($"Done. {results.Count} executions, {results.Count(r => r.Error is not null)} errors. p50={p50}ms p95={p95}ms");
}
Console.WriteLine($"CSV reports written to '{options.OutputFolder}'.");
return 0;

/// <summary>Command-line options for a single stress-test run.</summary>
internal sealed record RunOptions(
    string ConnectionString,
    string QueriesFolder,
    string OutputFolder,
    int Workers,
    int IterationsPerWorker)
{
    public static RunOptions? Parse(string[] args)
    {
        string? server = null;
        string database = "centeredge";
        string queriesFolder = Path.Combine(AppContext.BaseDirectory, "queries");
        string outputFolder = Path.Combine(Environment.CurrentDirectory, "results", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        int workers = 10;
        int iterations = 20;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--server" when i + 1 < args.Length: server = args[++i]; break;
                case "--database" when i + 1 < args.Length: database = args[++i]; break;
                case "--queries" when i + 1 < args.Length: queriesFolder = args[++i]; break;
                case "--output" when i + 1 < args.Length: outputFolder = args[++i]; break;
                case "--workers" when i + 1 < args.Length: workers = int.Parse(args[++i]); break;
                case "--iterations" when i + 1 < args.Length: iterations = int.Parse(args[++i]); break;
                case "--help" or "-h": return null;
            }
        }

        if (server is null)
        {
            Console.Error.WriteLine("Missing required --server <name\\instance>.");
            return null;
        }

        string connectionString = $"Server={server};Database={database};Integrated Security=true;TrustServerCertificate=true;";
        return new RunOptions(connectionString, queriesFolder, outputFolder, workers, iterations);
    }

    public static void PrintUsage()
    {
        Console.WriteLine("""
            Usage: HardwareTesting.StressTest --server <name\instance> [options]

            Options:
              --server <name\instance>   SQL Server to connect to (required), e.g. .\SQLEXPRESS
              --database <name>          Database name (default: centeredge)
              --queries <folder>         Folder of .sql files making up the workload mix (default: ./queries)
              --output <folder>          Folder to write CSV reports to (default: ./results/<timestamp>)
              --workers <n>              Number of concurrent simulated workstations (default: 10)
              --iterations <n>           Query executions per worker (default: 20)
            """);
    }
}
