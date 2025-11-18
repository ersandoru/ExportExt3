namespace ExportExt3.Models;

public sealed class DiskPartitionInfo
{
    public string DevicePath { get; init; } = string.Empty;

    public string DiskModel { get; init; } = string.Empty;

    public string PartitionId { get; init; } = string.Empty;

    public int Index { get; init; }

    public long Size { get; init; }

    public long StartingOffset { get; init; }

    public string? PartitionType { get; init; }

    public bool LooksLikeLinux { get; init; }

    public string DisplayName => $"Partition {Index} - {SizeFormatter.FormatBytes(Size)}";

    public override string ToString() => $"{DisplayName} ({PartitionType ?? "Unknown"})";
}
