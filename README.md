# ExportExt3

ExportExt3 is a WPF utility for browsing attached disks and exporting the contents of Ext3 partitions. It wraps DiscUtils to read Linux filesystems from Windows and provides progress reporting while the export runs.

## Features
- Enumerates physical disks and their partitions, including Ext3 ones.
- Exports files to a Windows directory while translating metadata.
- Reports progress and a final summary of successes/failures.

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

## Contributing
Issues and pull requests are welcome. Please include as much detail as possible when reporting problems so we can reproduce them quickly.
