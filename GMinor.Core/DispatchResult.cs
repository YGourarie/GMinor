namespace GMinor.Core;

/// <summary>The outcome of a single file dispatch operation.</summary>
public record DispatchResult
{
    /// <summary>The outcome category of the dispatch operation.</summary>
    public DispatchOutcome Outcome { get; init; }

    /// <summary>The full path of the source file.</summary>
    public required string SourcePath { get; init; }

    /// <summary>The full resolved destination path including filename, or <see langword="null"/> when not moved.</summary>
    public string? DestPath { get; init; }
}

/// <summary>Categorizes the outcome of a <see cref="FileDispatcher.Dispatch"/> call.</summary>
public enum DispatchOutcome
{
    /// <summary>The file was moved to its destination.</summary>
    Moved,

    /// <summary>The file was skipped (no routing match, or conflict resolved as Skip).</summary>
    Skipped,

    /// <summary>Dry-run mode was active; the file was not moved.</summary>
    DryRun,

    /// <summary>A conflicting destination file was overwritten.</summary>
    Overwritten,
}
