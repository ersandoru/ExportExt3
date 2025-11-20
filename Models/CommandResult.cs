namespace ExportExt3.Models;

public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);
