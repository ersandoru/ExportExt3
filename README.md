# ExportExt3

ExportExt3 is a WPF utility for browsing attached disks and exporting the contents of Ext3 partitions. It wraps DiscUtils to read Linux filesystems from Windows and provides progress reporting while the export runs.

## Features
- Enumerates physical disks and their partitions, including Ext3 ones.
- Exports files to a Windows directory while translating metadata.
- Reports progress and a final summary of successes/failures.
- Optional one-click WSL assist to install WSL and mount a removable ext3 partition read-only.

## Requirements
- Windows 10 or later.
- .NET 10.0 SDK (preview) to build and run the project from source.

## Getting Started
```powershell
git clone https://github.com/ersandoru/ExportExt3.git
cd ExportExt3
dotnet build
dotnet run
```

You can also open `ExportExt3.sln` with Visual Studio 2022 and use the built-in WPF designer/debugger.

## WSL auto-mount helper
If the ext3 SD card does not surface as a removable disk in Windows Explorer, you can let the app mount it via WSL:

1. Click **Install / repair WSL**. This runs `wsl --install --no-distribution`; Windows may ask for elevation or a reboot.
2. Select the SD card and its ext3 partition in the left panel.
3. Click **Mount ext3 in WSL**. The app issues a read-only `wsl --mount \\.\PHYSICALDRIVE# --partition N --type ext3` command and shows the expected mount point (for example `/mnt/wsl/PHYSICALDRIVE1p1`).
4. Open a WSL shell and browse the mount point. The partition is mounted read-only to avoid modifying the SD card.

## Contributing
Issues and pull requests are welcome. Please include as much detail as possible when reporting problems so we can reproduce them quickly.
