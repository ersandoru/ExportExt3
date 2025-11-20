using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ExportExt3.Models;

namespace ExportExt3.Services;

/// <summary>
/// Installs WSL if missing and mounts physical disks/partitions with ext3 read-only access.
/// All calls are best-effort and surface standard output/error so the caller can show actionable feedback.
/// </summary>
public sealed class WslService
{
    private const string WslExe = "wsl.exe";

    public async Task<WslStatus> GetStatusAsync()
    {
        try
        {
            var result = await RunAsync("--status");
            if (result.ExitCode != 0)
            {
                return new WslStatus(false,
                    $"WSL command returned {result.ExitCode}: {Coalesce(result.StandardError, result.StandardOutput)}");
            }

            var distro = ParseDefaultDistro(result.StandardOutput);
            return new WslStatus(true,
                $"WSL is installed. Default distro: {distro ?? "not set"}.",
                distro);
        }
        catch (Win32Exception)
        {
            return new WslStatus(false, "WSL is not installed on this machine.");
        }
        catch (Exception ex)
        {
            return new WslStatus(false, $"Unable to query WSL: {ex.Message}");
        }
    }

    public Task<CommandResult> InstallAsync()
    {
        // --no-distribution avoids pulling a specific distro; the user can add one later or keep only the kernel for mounting.
        return RunAsync("--install --no-distribution", 300000);
    }

    public Task<CommandResult> UpdateAsync()
    {
        // Updates the WSL kernel/install. Harmless if WSL is already healthy, useful for repair.
        return RunAsync("--update", 300000);
    }

    public Task<CommandResult> MountPartitionAsync(DiskPartitionInfo partition, bool readOnly = true)
    {
        var wslPartitionNumber = partition.WslPartitionNumber;
        var args = new StringBuilder();
        args.Append($"--mount {partition.DevicePath} --partition {wslPartitionNumber} --type ext3");
        if (readOnly)
        {
            args.Append(" --options \"ro\"");
        }

        // Mounting can take a while if the kernel has to spin up.
        return RunAsync(args.ToString(), 300000);
    }

    public string GetMountPointHint(DiskPartitionInfo partition)
    {
        var deviceName = Path.GetFileName(partition.DevicePath.Replace("\\\\", "\\", StringComparison.Ordinal));
        return $"/mnt/wsl/{deviceName}p{partition.WslPartitionNumber}";
    }

    private static async Task<CommandResult> RunAsync(string arguments, int timeoutMilliseconds = 300000)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = WslExe,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stdout.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var completedTask = await Task.WhenAny(process.WaitForExitAsync(), Task.Delay(timeoutMilliseconds));
        if (completedTask is Task delayTask && delayTask.IsCompleted)
        {
            try
            {
                process.Kill(true);
            }
            catch
            {
                // Ignore any failure to kill as we are already timing out.
            }

            var message =
                $"WSL command timed out after {timeoutMilliseconds} ms. If this was the first time running WSL, open an elevated terminal and run \"wsl --status\" once, then retry.";
            return new CommandResult(-1, stdout.ToString(), message);
        }

        return new CommandResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static string? ParseDefaultDistro(string output)
    {
        using var reader = new StringReader(output);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.StartsWith("Default Distribution", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(':', 2);
                if (parts.Length == 2)
                {
                    return parts[1].Trim();
                }
            }
        }

        return null;
    }

    private static string Coalesce(string primary, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary.Trim();
        }

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback.Trim();
        }

        return "Unknown WSL response.";
    }
}
