namespace ExportExt3.Models;

public sealed record WslStatus(bool IsInstalled, string Message, string? DefaultDistribution = null);
