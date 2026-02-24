using GMinor.Core;
using GMinor.Core.Exceptions;
using GMinor.Core.Rules;
using GMinor.Console;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;

// ── Configuration ────────────────────────────────────────────────────────────

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var sourceFolder = config["FileRouter:SourceFolder"]
    ?? throw new InvalidOperationException("FileRouter:SourceFolder is not configured.");

var destFolder = config["FileRouter:DestinationFolder"]
    ?? throw new InvalidOperationException("FileRouter:DestinationFolder is not configured.");

bool dryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);

// ── Logging ───────────────────────────────────────────────────────────────────

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        Path.Combine(AppContext.BaseDirectory, "logs", "gminor-.log"),
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddSerilog(Log.Logger, dispose: false));

var logger = loggerFactory.CreateLogger("GMinor.Console");

// ── Dispatch ──────────────────────────────────────────────────────────────────

if (!Directory.Exists(sourceFolder))
{
    logger.LogError("Source folder does not exist: {SourceFolder}", sourceFolder);
    return 1;
}

if (dryRun)
    logger.LogInformation("Dry-run mode enabled — no files will be moved.");

var dispatcher = new FileDispatcher(f => RoutingRules.Route(f, destFolder), loggerFactory.CreateLogger<FileDispatcher>());
var conflictResolver = new InteractiveConflictResolver();

int moved = 0, skipped = 0, overwritten = 0, dryRunCount = 0;

foreach (var filePath in Directory.EnumerateFiles(sourceFolder, "*", SearchOption.TopDirectoryOnly))
{
    try
    {
        var result = dispatcher.Dispatch(filePath, dryRun, conflictResolver);

        switch (result.Outcome)
        {
            case DispatchOutcome.Moved:       moved++;       break;
            case DispatchOutcome.Skipped:     skipped++;     break;
            case DispatchOutcome.Overwritten: overwritten++; break;
            case DispatchOutcome.DryRun:      dryRunCount++; break;
        }
    }
    catch (FileLockedException ex)
    {
        logger.LogError("File is locked and cannot be moved: {FilePath}", ex.FilePath);
        return 1;
    }
}

// ── Summary ───────────────────────────────────────────────────────────────────

logger.LogInformation(
    "Done. Moved: {Moved} | Skipped: {Skipped} | Overwritten: {Overwritten} | Dry-run: {DryRun}",
    moved, skipped, overwritten, dryRunCount);

return 0;
