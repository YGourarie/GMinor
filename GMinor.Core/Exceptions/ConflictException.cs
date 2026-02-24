namespace GMinor.Core.Exceptions;

/// <summary>
/// Thrown when a file already exists at the destination and no
/// <see cref="GMinor.Core.Conflicts.IConflictResolver"/> was provided to handle it.
/// </summary>
public class ConflictException : IOException
{
    /// <summary>Initializes a new instance with source and destination paths.</summary>
    /// <param name="sourcePath">Full path of the source file being dispatched.</param>
    /// <param name="destinationPath">Full path of the conflicting destination file.</param>
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
