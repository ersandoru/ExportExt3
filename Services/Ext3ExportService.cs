using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DiscUtils.Ext;
using DiscUtils;
using DiscUtils.Streams;
using ExportExt3.Models;

namespace ExportExt3.Services;

public sealed class Ext3ExportService
{
    public Task<ExportSummary> ExportPartitionAsync(
        DiskPartitionInfo partition,
        string destinationFolder,
        IProgress<ExportProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(partition);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFolder);

        Directory.CreateDirectory(destinationFolder);

        return Task.Run(() =>
        {
            var stats = new ExportAccumulator(partition.Size);
            using var deviceStream = new FileStream(
                partition.DevicePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);

            var sparseStream = SparseStream.FromStream(deviceStream, Ownership.None);
            using var partitionStream = new SubStream(sparseStream, partition.StartingOffset, partition.Size);
            using var fileSystem = new ExtFileSystem(partitionStream);

            ExportDirectory(fileSystem.Root, destinationFolder, stats, progress, cancellationToken);
            return stats.ToSummary();
        }, cancellationToken);
    }

    private static void ExportDirectory(
        DiscDirectoryInfo directory,
        string destinationRoot,
        ExportAccumulator stats,
        IProgress<ExportProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var relativePath = NormalizeRelativePath(directory.FullName);
        var destinationPath = string.IsNullOrEmpty(relativePath)
            ? destinationRoot
            : Path.Combine(destinationRoot, relativePath);

        Directory.CreateDirectory(destinationPath);

        if (!string.IsNullOrEmpty(relativePath))
        {
            stats.DirectoriesCreated++;
        }

        foreach (var subDirectory in directory.GetDirectories())
        {
            ExportDirectory(subDirectory, destinationRoot, stats, progress, cancellationToken);
        }

        foreach (var file in directory.GetFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileRelativePath = NormalizeRelativePath(file.FullName);
            var destinationFile = Path.Combine(destinationRoot, fileRelativePath);
            var destinationFolder = Path.GetDirectoryName(destinationFile);
            if (!string.IsNullOrEmpty(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            using var sourceStream = file.Open(FileMode.Open, FileAccess.Read);
            using var destinationStream = File.Create(destinationFile);
            sourceStream.CopyTo(destinationStream);

            stats.FilesCopied++;
            stats.BytesCopied += sourceStream.Length;

            progress?.Report(new ExportProgressReport(
                stats.FilesCopied,
                stats.DirectoriesCreated,
                stats.BytesCopied,
                stats.ProgressPercentage,
                file.FullName));
        }
    }

    private static string NormalizeRelativePath(string path)
    {
        var trimmed = path.TrimStart('\\').Replace('/', Path.DirectorySeparatorChar);
        return trimmed;
    }

    private sealed class ExportAccumulator
    {
        private readonly long _partitionSize;
        private readonly DateTime _startTime = DateTime.UtcNow;

        public ExportAccumulator(long partitionSize)
        {
            _partitionSize = partitionSize <= 0 ? 1 : partitionSize;
        }

        public long FilesCopied { get; set; }

        public long DirectoriesCreated { get; set; }

        public long BytesCopied { get; set; }

        public double ProgressPercentage => Math.Clamp(BytesCopied / (double)_partitionSize * 100d, 0d, 100d);

        public ExportSummary ToSummary()
        {
            var elapsed = DateTime.UtcNow - _startTime;
            return new ExportSummary(FilesCopied, DirectoriesCreated, BytesCopied, elapsed);
        }
    }
}
