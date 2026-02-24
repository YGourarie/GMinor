namespace GMinor.Core.Conflicts;

/// <summary>
/// Determines how to handle a naming conflict when a file already exists at the destination.
/// </summary>
public interface IConflictResolver
{
    /// <summary>
    /// Resolves a conflict between a source file and an existing destination file.
    /// </summary>
    /// <param name="sourcePath">Full path of the file being dispatched.</param>
    /// <param name="destinationPath">Full path of the already-existing destination file.</param>
    /// <returns>
    /// <see cref="ConflictResolution.Overwrite"/> to replace the destination,
    /// or <see cref="ConflictResolution.Skip"/> to leave the source in place.
    /// </returns>
    ConflictResolution Resolve(string sourcePath, string destinationPath);
}
