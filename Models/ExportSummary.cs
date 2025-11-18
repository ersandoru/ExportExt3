using System;

namespace ExportExt3.Models;

public sealed record ExportSummary(
    long FilesCopied,
    long DirectoriesCreated,
    long BytesCopied,
    TimeSpan Duration);
