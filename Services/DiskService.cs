using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using ExportExt3.Models;

namespace ExportExt3.Services;

public sealed class DiskService
{
    public Task<IReadOnlyList<DiskDeviceInfo>> GetCandidateDisksAsync()
    {
        return Task.Run(() =>
        {
            var disks = new List<DiskDeviceInfo>();
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID, Model, Size, MediaType, InterfaceType FROM Win32_DiskDrive");

            foreach (ManagementObject drive in searcher.Get())
            {
                using (drive)
                {
                    var deviceId = Convert.ToString(drive["DeviceID"], CultureInfo.InvariantCulture) ?? string.Empty;
                    var model = Convert.ToString(drive["Model"], CultureInfo.InvariantCulture) ?? "Unknown drive";
                    var size = TryConvertToUInt64(drive["Size"]);
                    var interfaceType = Convert.ToString(drive["InterfaceType"], CultureInfo.InvariantCulture);
                    var mediaType = Convert.ToString(drive["MediaType"], CultureInfo.InvariantCulture);

                    var isRemovable = IsRemovable(interfaceType, mediaType);
                    if (!isRemovable)
                    {
                        continue;
                    }

                    var partitions = GetPartitions(deviceId, model).ToList();
                    if (partitions.Count == 0)
                    {
                        continue;
                    }

                    disks.Add(new DiskDeviceInfo
                    {
                        DevicePath = deviceId,
                        FriendlyName = model.Trim(),
                        Size = size,
                        IsRemovable = isRemovable,
                        Partitions = partitions
                    });
                }
            }

            return (IReadOnlyList<DiskDeviceInfo>)disks;
        });
    }

    private static IEnumerable<DiskPartitionInfo> GetPartitions(string deviceId, string model)
    {
        var escapedId = deviceId.Replace("\\", "\\\\", StringComparison.Ordinal);
        var query =
            $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{escapedId}'}} WHERE AssocClass = Win32_DiskDriveToDiskPartition";

        using var searcher = new ManagementObjectSearcher(query);
        foreach (ManagementObject partition in searcher.Get())
        {
            using (partition)
            {
                var type = Convert.ToString(partition["Type"], CultureInfo.InvariantCulture);
                var looksLikeLinux = type != null &&
                                     type.IndexOf("LINUX", StringComparison.OrdinalIgnoreCase) >= 0;

                yield return new DiskPartitionInfo
                {
                    DevicePath = deviceId,
                    DiskModel = model,
                    PartitionId = Convert.ToString(partition["DeviceID"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Index = Convert.ToInt32(partition["Index"], CultureInfo.InvariantCulture),
                    Size = (long)TryConvertToUInt64(partition["Size"]),
                    StartingOffset = (long)TryConvertToUInt64(partition["StartingOffset"]),
                    PartitionType = type,
                    LooksLikeLinux = looksLikeLinux
                };
            }
        }
    }

    private static ulong TryConvertToUInt64(object? input)
    {
        try
        {
            return Convert.ToUInt64(input, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private static bool IsRemovable(string? interfaceType, string? mediaType)
    {
        if (interfaceType != null &&
            interfaceType.Equals("USB", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (mediaType != null &&
            mediaType.IndexOf("REMOVABLE", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return false;
    }
}
