namespace GMinor.Core.Rules;

/// <summary>
/// All routing logic lives here. Extract variables from the filename, build the destination
/// path and name. Rules are evaluated top-to-bottom; return on first match.
/// Return <see cref="RoutingResult.NoMatch"/> if the file should be left in place.
/// </summary>
public static class RoutingRules
{
    /// <summary>
    /// Routes a filename to a destination directory and new name, rooted under
    /// <paramref name="destRoot"/>. Files are grouped into a subfolder named after their
    /// extension (lowercase), and the filename is prefixed with <c>moved-</c>.
    /// Files with no extension are placed in a <c>misc</c> subfolder.
    /// </summary>
    /// <param name="filename">The bare filename (no directory component) to evaluate.</param>
    /// <param name="destRoot">The root destination folder configured by the host.</param>
    /// <returns>
    /// A <see cref="RoutingResult"/> describing where the file should go.
    /// Always matches — returns <see cref="RoutingResult.NoMatch"/> only for empty filenames.
    /// </returns>
    public static RoutingResult Route(string filename, string destRoot)
    {
        if (string.IsNullOrEmpty(filename))
            return RoutingResult.NoMatch;

        var ext = Path.GetExtension(filename).TrimStart('.').ToLowerInvariant();
        var folder = string.IsNullOrEmpty(ext) ? "misc" : ext;

        return new RoutingResult
        {
            DestDir  = Path.Combine(destRoot, folder),
            DestName = "moved-" + filename,
        };
    }
}
