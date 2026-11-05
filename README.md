# HardwareTesting — Advantage SQL Server stress test

Hardware benchmarking tool for CenterEdge Advantage: runs a configurable query mix
against SQL Server from multiple simulated workstations, while sampling CPU/RAM/disk
and SQL wait stats, and exporting everything to CSV for comparison across machines.

## Status

Moved here from an earlier CLI session. Project context so far:

- **Target stack:** C#/.NET, client runs Windows 11, server runs Windows Server 2025.
- **Database:** `centeredge` (real Advantage DB). A sample backup `00Pathfinder.bak`
  (gitignored, not tracked) was restored locally as `TH_Pathfinder` on SQL Server 2025
  Express to inspect schema: 644 tables, 1,037 indexes, 636 FKs, 65 procs, 101 views.
  Largest tables are mostly empty in this sample (`InvSnapShot` 441k rows/24 MB,
  `AreaHistory` 135k, `SubCatSales` 98k, `Receipt` 2.5k) — scaling to 1–25 GB test
  databases means growing transaction/history tables (`Receipt`, `RecLines`, `Till`,
  `TicketTrans`, `InvSnapShot`, `AreaHistory`, `SubCatSales`, `PlayerCardTrans`, ...).
- **Output:** CSV reports — hardware inventory, per-query timings, periodic
  CPU/RAM/disk samples, and SQL wait-stat deltas (`sys.dm_os_wait_stats` before/after).
- **Workload source:** `CaptureWorkload.sql` is an Extended Events session that
  captures real `rpc_completed`/`sql_batch_completed` events for the `centeredge`
  database (filtered to exclude SSMS/sqlcmd/Profiler noise). Run it in SSMS as
  sysadmin, use Advantage normally for 15–60 min, then stop/drop it and pull
  `AdvantageWorkload*.xel` from the instance's `...\MSSQL\Log` folder. SQL Express
  only has shared memory enabled by default — connect with `lpc:.\SQLEXPRESS` in
  sqlcmd, or a plain `.\SQLEXPRESS` connection string from .NET. Enable TCP/IP in
  SQL Server Configuration Manager if testing needs to run across the network.
- **Relevant Advantage ports** (for firewall setup on new test machines):
  inbound 1433 (SQL), 15050–15060 (License/Fingerprint/Signage/Embed/API/Stage),
  31420 (Credit Cards), 58008 (Embed); outbound 443, 50511, 50512, 9000, 9100.

## Still open (unanswered from the original session)

1. `manager-console-startup-sql.md` (code-derived, unverified) gave two real, literal
   queries now seeded in `queries/` — a one-time version check and app-info lookup.
   That only covers Manager Console **startup**, not the repeated, concurrent
   operations that actually drive load: POS sales, ticket check-in/out, end-of-day,
   reports. A live trace (`CaptureWorkload.sql`, run against a live Advantage install,
   `.xel` dropped in this repo) is still needed for those — or the same
   code-derived-SQL approach applied to those code paths instead of a live trace.
2. Pass/fail targets (e.g. POS transaction < 1s, reports < 10s at p95) are not defined.
3. Exact CPU models for the test matrix are not defined.
4. Whether the tool should also exercise the Advantage services (ports 15050-15060,
   31420) directly, or SQL only.

## Project layout

- `src/HardwareTesting.StressTest/` — the .NET console app.
  - `HardwareInventoryCollector.cs` — one-time CPU/RAM/disk facts via WMI.
  - `ResourceSampler.cs` — periodic CPU/RAM/disk perf-counter samples during a run.
  - `SqlWaitStatsCollector.cs` — before/after `sys.dm_os_wait_stats` snapshot + diff.
  - `QueryRunner.cs` — runs a query mix with N concurrent simulated workstations.
  - `CsvReportWriter.cs` — writes all of the above to CSV.
  - `queries/*.sql` — the workload mix; one query per file, replace with real ones.
- `CaptureWorkload.sql` — Extended Events script to capture a real Advantage workload.
- `Trace.tdf` — SQL Profiler template (events/columns only, not a captured trace).
- `00Pathfinder.bak` — sample DB backup used to inspect schema (gitignored).

## Running

This dev machine doesn't need the .NET SDK — testing happens on separate machines.
Install the SDK on each test machine before running. Requires SQL Server reachable
with Windows auth.

```powershell
cd src/HardwareTesting.StressTest
dotnet run -- --server .\SQLEXPRESS --database centeredge --workers 10 --iterations 20
```

Reports land in `results/<timestamp>/` as `hardware-inventory.csv`,
`query-results.csv`, `resource-samples.csv`, and `wait-stats.csv`.
