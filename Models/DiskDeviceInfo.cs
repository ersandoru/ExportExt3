using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ExportExt3.Models;

public sealed class DiskDeviceInfo
{
    public string DevicePath { get; init; } = string.Empty;

    public string FriendlyName { get; init; } = string.Empty;

    public ulong Size { get; init; }

    public bool IsRemovable { get; init; }

    public IReadOnlyList<DiskPartitionInfo> Partitions { get; init; } = new ReadOnlyCollection<DiskPartitionInfo>(new List<DiskPartitionInfo>());

    public bool HasLinuxHint => Partitions.Any(p => p.LooksLikeLinux);

    public override string ToString()
    {
        return $"{FriendlyName} ({SizeFormatter.FormatBytes((long)Size)})";
    }
}
