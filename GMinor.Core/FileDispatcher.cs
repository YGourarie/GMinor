using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GMinor.Core;

// ── Conflict types ────────────────────────────────────────────────────────────

/// <summary>Determines how a naming conflict at the destination should be resolved.</summary>
public enum ConflictResolution { Overwrite, Skip }

/// <summary>
/// Determines how to handle a naming conflict when a file already exists at the destination.
/// </summary>
public interface IConflictResolver
{
    /// <summary>
    /// Resolves a conflict between a source file and an existing destination file.
    /// </summary>
    /// <returns>
    /// <see cref="ConflictResolution.Overwrite"/> to replace the destination,
    /// or <see cref="ConflictResolution.Skip"/> to leave the source in place.
    /// </returns>
    ConflictResolution Resolve(string sourcePath, string destinationPath);
}

// ── Exceptions ────────────────────────────────────────────────────────────────

/// <summary>
/// Thrown when the source file is locked or in use by another process.
/// </summary>
public class FileLockedException : IOException
{
    /// <summary>Initializes a new instance with the path of the locked file.</summary>
    public FileLockedException(string filePath, IOException inner)
        : base($"File is locked or in use and cannot be moved: {filePath}", inner)
    {
        FilePath = filePath;
    }

    /// <summary>Gets the full path of the file that was locked.</summary>
    public string FilePath { get; }
}

/// <summary>
/// Thrown when a file already exists at the destination and no
/// <see cref="IConflictResolver"/> was provided to handle it.
/// </summary>
public class ConflictException : IOException
{
    /// <summary>Initializes a new instance with source and destination paths.</summary>
    public ConflictException(string sourcePath, string destinationPath)
        : base($"Destination file already exists and no conflict resolver was provided. Source: '{sourcePath}', Destination: '{destinationPath}'")
    {
        SourcePath = sourcePath;
        DestinationPath = destinationPath;
    }

    /// <summary>Gets the full path of the source file.</summary>
    public string SourcePath { get; }

    /// <summary>Gets the full path of the conflicting destination file.</summary>
    public string DestinationPath { get; }
}

// ── Dispatcher ────────────────────────────────────────────────────────────────

/// <summary>
/// Routes and moves individual files using a caller-supplied routing function.
/// </summary>
public class FileDispatcher
{
    private readonly Func<string, RoutingResult> _routingFunc;
    private readonly ILogger<FileDispatcher> _logger;

    /// <summary>
    /// Initializes a new <see cref="FileDispatcher"/> with the given routing function.
    /// </summary>
    /// <param name="routingFunc">
    /// The function that maps a bare filename to a <see cref="RoutingResult"/>.
    /// </param>
    /// <param name="logger">Optional logger. Defaults to a no-op logger.</param>
    public FileDispatcher(Func<string, RoutingResult> routingFunc, ILogger<FileDispatcher>? logger = null)
    {
        _routingFunc = routingFunc;
        _logger = logger ?? NullLogger<FileDispatcher>.Instance;
    }

    /// <summary>
    /// Dispatches a single file: routes it according to the routing function, then moves it.
    /// </summary>
    /// <param name="filePath">Full path of the source file to dispatch.</param>
    /// <param name="dryRun">
    /// When <see langword="true"/>, logs what would happen but performs no file I/O.
    /// </param>
    /// <param name="resolver">
    /// Conflict resolver invoked when a file already exists at the destination.
    /// If <see langword="null"/> and a conflict occurs, <see cref="ConflictException"/> is thrown.
    /// </param>
    /// <returns>A <see cref="DispatchResult"/> describing what happened.</returns>
    /// <exception cref="FileLockedException">The source file is locked or in use.</exception>
    /// <exception cref="ConflictException">
    /// A file exists at the destination and <paramref name="resolver"/> is <see langword="null"/>.
    /// </exception>
    public DispatchResult Dispatch(string filePath, bool dryRun = false, IConflictResolver? resolver = null)
    {
        var filename = Path.GetFileName(filePath);
        var routing = _routingFunc(filename);

        if (!routing.IsMatch)
        {
            _logger.LogWarning("No routing rule matched file '{FileName}'. Leaving in place.", filename);
            return new DispatchResult { Outcome = DispatchOutcome.Skipped, SourcePath = filePath };
        }

        var destName = routing.DestName ?? filename;
        var destPath = Path.Combine(routing.DestDir!, destName);

        if (dryRun)
        {
            _logger.LogInformation("[DRY RUN] Would move '{Source}' → '{Dest}'", filePath, destPath);
            return new DispatchResult { Outcome = DispatchOutcome.DryRun, SourcePath = filePath, DestPath = destPath };
        }

        if (File.Exists(destPath))
        {
            if (resolver is null)
                throw new ConflictException(filePath, destPath);

            var resolution = resolver.Resolve(filePath, destPath);

            if (resolution == ConflictResolution.Skip)
            {
                _logger.LogWarning("Conflict: '{Dest}' already exists. Skipping '{Source}'.", destPath, filePath);
                return new DispatchResult { Outcome = DispatchOutcome.Skipped, SourcePath = filePath, DestPath = destPath };
            }

            _logger.LogInformation("Conflict resolved as Overwrite: replacing '{Dest}' with '{Source}'.", destPath, filePath);
            MoveFile(filePath, destPath, overwrite: true);
            return new DispatchResult { Outcome = DispatchOutcome.Overwritten, SourcePath = filePath, DestPath = destPath };
        }

        MoveFile(filePath, destPath, overwrite: false);
        _logger.LogInformation("Moved '{Source}' → '{Dest}'", filePath, destPath);
        return new DispatchResult { Outcome = DispatchOutcome.Moved, SourcePath = filePath, DestPath = destPath };
    }

    private void MoveFile(string source, string dest, bool overwrite)
    {
        var destDir = Path.GetDirectoryName(dest)!;
        if (!Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        try
        {
            File.Move(source, dest, overwrite);
        }
        catch (IOException ex) when (IsFileLocked(ex))
        {
            throw new FileLockedException(source, ex);
        }
    }

    private static bool IsFileLocked(IOException ex)
    {
        const int sharingViolation = unchecked((int)0x80070020);
        const int lockViolation    = unchecked((int)0x80070021);
        return ex.HResult == sharingViolation || ex.HResult == lockViolation;
    }
}
