namespace GMinor.Core;

/// <summary>The output of a routing decision produced by the routing function.</summary>
public record RoutingResult
{
    /// <summary>Destination directory. <see langword="null"/> means no match.</summary>
    public string? DestDir { get; init; }

    /// <summary>
    /// Destination filename. If <see langword="null"/>, the original filename is preserved.
    /// </summary>
    public string? DestName { get; init; }

    /// <summary>Gets a value indicating whether this result represents a routing match.</summary>
    public bool IsMatch => DestDir is not null;

    /// <summary>Sentinel value representing a no-match routing decision.</summary>
    public static readonly RoutingResult NoMatch = new();
}
