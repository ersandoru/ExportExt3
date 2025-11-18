using System;

namespace ExportExt3;

internal static class SizeFormatter
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB", "PB" };

    public static string FormatBytes(long value)
    {
        if (value < 0)
        {
            return "0 B";
        }

        double size = value;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < Units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {Units[unitIndex]}";
    }
}
