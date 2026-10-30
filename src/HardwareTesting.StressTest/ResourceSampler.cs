using System.Diagnostics;

namespace HardwareTesting.StressTest;

/// <summary>Samples OS-level CPU/RAM/disk counters on a timer while a workload runs.</summary>
public sealed class ResourceSampler : IDisposable
{
    private readonly PerformanceCounter _cpuCounter = new("Processor", "% Processor Time", "_Total");
    private readonly PerformanceCounter _availableRamCounter = new("Memory", "Available MBytes");
    private readonly PerformanceCounter _diskIdleCounter = new("PhysicalDisk", "% Idle Time", "_Total");
    private readonly PerformanceCounter _diskQueueCounter = new("PhysicalDisk", "Current Disk Queue Length", "_Total");
    private readonly List<ResourceSample> _samples = [];
    private readonly Timer _timer;
    private readonly Lock _lock = new();

    public ResourceSampler(TimeSpan interval)
    {
        // Prime the counters; the first NextValue() call after construction is always 0.
        _cpuCounter.NextValue();
        _diskIdleCounter.NextValue();
        _timer = new Timer(_ => Sample(), null, interval, interval);
    }

    private void Sample()
    {
        var sample = new ResourceSample(
            TimestampUtc: DateTimeOffset.UtcNow,
            CpuPercent: Math.Round(_cpuCounter.NextValue(), 1),
            AvailableRamGb: Math.Round(_availableRamCounter.NextValue() / 1024.0, 2),
            DiskPercentIdle: Math.Round(_diskIdleCounter.NextValue(), 1),
            DiskQueueLength: Math.Round(_diskQueueCounter.NextValue(), 2));

        lock (_lock)
        {
            _samples.Add(sample);
        }
    }

    public IReadOnlyList<ResourceSample> GetSamples()
    {
        lock (_lock)
        {
            return [.. _samples];
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
        _cpuCounter.Dispose();
        _availableRamCounter.Dispose();
        _diskIdleCounter.Dispose();
        _diskQueueCounter.Dispose();
    }
}
