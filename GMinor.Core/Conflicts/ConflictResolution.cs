namespace GMinor.Core.Conflicts;

/// <summary>Determines how a naming conflict at the destination should be resolved.</summary>
public enum ConflictResolution
{
    /// <summary>Overwrite the existing destination file.</summary>
    Overwrite,

    /// <summary>Leave the source file in place and skip the move.</summary>
    Skip,
}
