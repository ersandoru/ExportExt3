using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ExportExt3.Models;
using ExportExt3.Services;
using Forms = System.Windows.Forms;

namespace ExportExt3;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly DiskService _diskService = new();
    private readonly Ext3ExportService _exportService = new();
    private readonly WslService _wslService = new();
    private readonly ObservableCollection<DiskDeviceInfo> _disks = new();
    private readonly ObservableCollection<DiskPartitionInfo> _partitions = new();

    private DiskDeviceInfo? _selectedDisk;
    private DiskPartitionInfo? _selectedPartition;
    private string? _destinationFolder;
    private string _statusMessage = "Ready.";
    private string _wslStatusMessage = "WSL status not checked yet.";
    private bool _isBusy;
    private bool _isWslBusy;
    private double _progressValue;
    private WslStatus _wslStatus = new(false, "WSL status not checked yet.");

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        Loaded += async (_, _) =>
        {
            DestinationFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "Ext3Export");
            await RefreshDisksAsync();
            await RefreshWslStatusAsync();
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DiskDeviceInfo> Disks => _disks;

    public ObservableCollection<DiskPartitionInfo> AvailablePartitions => _partitions;

    public DiskDeviceInfo? SelectedDisk
    {
        get => _selectedDisk;
        set
        {
            if (!Equals(_selectedDisk, value))
            {
                _selectedDisk = value;
                OnPropertyChanged();
                UpdatePartitions();
                OnPropertyChanged(nameof(CanExport));
                OnPropertyChanged(nameof(CanMountWithWsl));
                OnPropertyChanged(nameof(WslMountCommandHint));
            }
        }
    }

    public DiskPartitionInfo? SelectedPartition
    {
        get => _selectedPartition;
        set
        {
            if (!Equals(_selectedPartition, value))
            {
                _selectedPartition = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanExport));
                OnPropertyChanged(nameof(CanMountWithWsl));
                OnPropertyChanged(nameof(WslMountCommandHint));
            }
        }
    }

    public string? DestinationFolder
    {
        get => _destinationFolder;
        set
        {
            if (_destinationFolder != value)
            {
                _destinationFolder = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanExport));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public string WslStatusMessage
    {
        get => _wslStatusMessage;
        set
        {
            if (_wslStatusMessage != value)
            {
                _wslStatusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanExport));
                OnPropertyChanged(nameof(IsProgressIndeterminate));
            }
        }
    }

    public bool IsWslBusy
    {
        get => _isWslBusy;
        set
        {
            if (_isWslBusy != value)
            {
                _isWslBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanMountWithWsl));
            }
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        set
        {
            if (Math.Abs(_progressValue - value) > double.Epsilon)
            {
                _progressValue = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsProgressIndeterminate));
            }
        }
    }

    public bool CanExport => !IsBusy &&
                             SelectedPartition is not null &&
                             !string.IsNullOrWhiteSpace(DestinationFolder);

    public bool CanMountWithWsl => !IsWslBusy && SelectedPartition is not null && _wslStatus.IsInstalled;

    public string WslMountCommandHint =>
        SelectedPartition is null
            ? "Select a partition to build a WSL mount command."
            : $"Planned mount: wsl --mount {SelectedPartition.DevicePath} --partition {SelectedPartition.WslPartitionNumber} --type ext3 --options \"ro\" (mount point {_wslService.GetMountPointHint(SelectedPartition)})";

    public bool IsProgressIndeterminate => IsBusy && ProgressValue < 0.1;

    private async Task RefreshDisksAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Scanning for removable disks...";
            Disks.Clear();
            AvailablePartitions.Clear();
            SelectedPartition = null;

            var disks = await _diskService.GetCandidateDisksAsync();
            foreach (var disk in disks)
            {
                Disks.Add(disk);
            }

            if (Disks.Count == 0)
            {
                StatusMessage = "No removable disks detected. Insert the SD card and press Refresh.";
                SelectedDisk = null;
            }
            else
            {
                SelectedDisk = Disks[0];
                StatusMessage = "Select the partition that contains your files.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to enumerate disks: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdatePartitions()
    {
        AvailablePartitions.Clear();
        if (SelectedDisk is null)
        {
            OnPropertyChanged(nameof(WslMountCommandHint));
            OnPropertyChanged(nameof(CanMountWithWsl));
            return;
        }

        foreach (var partition in SelectedDisk.Partitions)
        {
            AvailablePartitions.Add(partition);
        }

        SelectedPartition = AvailablePartitions.Count switch
        {
            0 => null,
            _ => AvailablePartitions.FirstOrDefault(p => p.LooksLikeLinux) ?? AvailablePartitions[0]
        };

        OnPropertyChanged(nameof(WslMountCommandHint));
        OnPropertyChanged(nameof(CanMountWithWsl));
    }

    private async Task RefreshWslStatusAsync()
    {
        try
        {
            IsWslBusy = true;
            WslStatusMessage = "Checking WSL status...";
            _wslStatus = await _wslService.GetStatusAsync();
            WslStatusMessage = _wslStatus.Message;
        }
        catch (Exception ex)
        {
            _wslStatus = new WslStatus(false, $"Unable to check WSL: {ex.Message}");
            WslStatusMessage = _wslStatus.Message;
        }
        finally
        {
            IsWslBusy = false;
            OnPropertyChanged(nameof(CanMountWithWsl));
        }
    }

    private async void RefreshClicked(object sender, RoutedEventArgs e)
    {
        await RefreshDisksAsync();
    }

    private void BrowseClicked(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Select the folder where files will be copied"
        };

        if (!string.IsNullOrWhiteSpace(DestinationFolder))
        {
            dialog.SelectedPath = DestinationFolder;
        }

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            DestinationFolder = dialog.SelectedPath;
        }
    }

    private async void ExportClicked(object sender, RoutedEventArgs e)
    {
        if (!CanExport || SelectedPartition is null || string.IsNullOrWhiteSpace(DestinationFolder))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(DestinationFolder);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"Unable to create destination folder: {ex.Message}", "ExportExt3",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        ProgressValue = 0;
        IsBusy = true;
        StatusMessage = "Starting export...";

        var progress = new Progress<ExportProgressReport>(report =>
        {
            ProgressValue = report.Percentage;
            StatusMessage = $"Copying {report.CurrentPath} ({report.Percentage:0.0}% complete)";
        });

        try
        {
            var summary = await _exportService.ExportPartitionAsync(
                SelectedPartition,
                DestinationFolder,
                progress,
                CancellationToken.None);

            ProgressValue = 100;
            StatusMessage =
                $"Export complete. {summary.FilesCopied} files, {summary.DirectoriesCreated} folders, {SizeFormatter.FormatBytes(summary.BytesCopied)} copied.";
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "Access denied when reading the SD card. Please run as Administrator.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void InstallWslClicked(object sender, RoutedEventArgs e)
    {
        if (IsWslBusy)
        {
            return;
        }

        try
        {
            IsWslBusy = true;
            WslStatusMessage = "Installing/repairing WSL (this may take a few minutes)...";
            var installResult = await _wslService.InstallAsync();
            if (installResult.ExitCode != 0)
            {
                WslStatusMessage =
                    $"WSL install failed (code {installResult.ExitCode}). {CombineOutput(installResult)}";
                return;
            }

            var updateResult = await _wslService.UpdateAsync();
            if (updateResult.ExitCode != 0)
            {
                WslStatusMessage =
                    $"WSL update failed (code {updateResult.ExitCode}). {CombineOutput(updateResult)}";
                return;
            }

            WslStatusMessage =
                "WSL install/update command completed. If this is the first install, reboot before mounting.";
        }
        catch (Exception ex)
        {
            WslStatusMessage = $"Unable to run WSL installer: {ex.Message}";
        }
        finally
        {
            IsWslBusy = false;
            await RefreshWslStatusAsync();
        }
    }

    private async void MountWithWslClicked(object sender, RoutedEventArgs e)
    {
        if (!CanMountWithWsl || SelectedPartition is null)
        {
            return;
        }

        try
        {
            IsWslBusy = true;
            WslStatusMessage = "Mounting in WSL as read-only ext3...";
            var result = await _wslService.MountPartitionAsync(SelectedPartition, readOnly: true);
            if (result.ExitCode == 0)
            {
                var mountPoint = _wslService.GetMountPointHint(SelectedPartition);
                WslStatusMessage =
                    $"Mounted in WSL at {mountPoint}. Open WSL and browse that path (read-only).";
            }
            else
            {
                WslStatusMessage = $"Mount failed (code {result.ExitCode}): {CombineOutput(result)}";
            }
        }
        catch (Exception ex)
        {
            WslStatusMessage = $"Mount failed: {ex.Message}";
        }
        finally
        {
            IsWslBusy = false;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string CombineOutput(CommandResult result)
    {
        var output = string.Empty;
        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            output = result.StandardOutput.Trim();
        }

        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            if (!string.IsNullOrEmpty(output))
            {
                output += " ";
            }

            output += result.StandardError.Trim();
        }

        return string.IsNullOrWhiteSpace(output)
            ? "No output from WSL."
            : output;
    }
}
