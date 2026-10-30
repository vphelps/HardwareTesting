using System.Management;

namespace HardwareTesting.StressTest;

/// <summary>Collects one-time hardware facts via WMI so results can be compared across test machines.</summary>
public static class HardwareInventoryCollector
{
    public static HardwareInventory Collect()
    {
        string cpuModel = "Unknown";
        int physicalCores = 0;
        int logicalCores = Environment.ProcessorCount;

        using (var cpuSearcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores FROM Win32_Processor"))
        {
            foreach (var obj in cpuSearcher.Get())
            {
                cpuModel = obj["Name"]?.ToString()?.Trim() ?? cpuModel;
                physicalCores += Convert.ToInt32(obj["NumberOfCores"] ?? 0);
            }
        }

        double ramTotalGb = 0;
        int ramSpeedMhz = 0;
        using (var memSearcher = new ManagementObjectSearcher("SELECT Capacity, Speed FROM Win32_PhysicalMemory"))
        {
            foreach (var obj in memSearcher.Get())
            {
                ramTotalGb += Convert.ToInt64(obj["Capacity"] ?? 0) / (1024.0 * 1024 * 1024);
                ramSpeedMhz = Convert.ToInt32(obj["Speed"] ?? ramSpeedMhz);
            }
        }

        var disks = new List<DiskInfo>();
        using (var diskSearcher = new ManagementObjectSearcher("SELECT Model, MediaType, Size FROM Win32_DiskDrive"))
        {
            foreach (var obj in diskSearcher.Get())
            {
                disks.Add(new DiskInfo(
                    Model: obj["Model"]?.ToString()?.Trim() ?? "Unknown",
                    MediaType: obj["MediaType"]?.ToString()?.Trim() ?? "Unknown",
                    SizeGb: Convert.ToInt64(obj["Size"] ?? 0) / (1024.0 * 1024 * 1024)));
            }
        }

        return new HardwareInventory(
            MachineName: Environment.MachineName,
            OsVersion: Environment.OSVersion.VersionString,
            CpuModel: cpuModel,
            PhysicalCores: physicalCores,
            LogicalCores: logicalCores,
            RamTotalGb: Math.Round(ramTotalGb, 1),
            RamSpeedMhz: ramSpeedMhz,
            Disks: disks,
            CollectedAtUtc: DateTimeOffset.UtcNow);
    }
}
