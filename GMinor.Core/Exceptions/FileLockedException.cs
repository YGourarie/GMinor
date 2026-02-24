namespace GMinor.Core.Exceptions;

/// <summary>
/// Thrown when the source file is locked or in use by another process,
/// preventing it from being moved.
/// </summary>
public class FileLockedException : IOException
{
    /// <summary>Initializes a new instance with the path of the locked file.</summary>
    /// <param name="filePath">Full path of the locked source file.</param>
    /// <param name="inner">The underlying I/O exception that caused the lock.</param>
    public FileLockedException(string filePath, IOException inner)
        : base($"File is locked or in use and cannot be moved: {filePath}", inner)
    {
        FilePath = filePath;
    }

    /// <summary>Gets the full path of the file that was locked.</summary>
    public string FilePath { get; }
}
