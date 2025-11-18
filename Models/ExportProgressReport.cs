namespace ExportExt3.Models;

public sealed record ExportProgressReport(
    long FilesCopied,
    long DirectoriesCreated,
    long BytesCopied,
    double Percentage,
    string? CurrentPath);
