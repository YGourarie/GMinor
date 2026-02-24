using GMinor.Core;

namespace GMinor.Console;

/// <summary>
/// An interactive conflict resolver that prompts the user at the terminal
/// to choose between overwriting or skipping a conflicting destination file.
/// </summary>
public class InteractiveConflictResolver : IConflictResolver
{
    /// <inheritdoc/>
    public ConflictResolution Resolve(string sourcePath, string destinationPath)
    {
        System.Console.Write($"⚠ File already exists at '{destinationPath}'. [O]verwrite / [S]kip? ");

        while (true)
        {
            var key = System.Console.ReadKey(intercept: true);
            System.Console.WriteLine();

            if (key.Key == ConsoleKey.O)
                return ConflictResolution.Overwrite;

            if (key.Key == ConsoleKey.S)
                return ConflictResolution.Skip;

            System.Console.Write("  Please press O to overwrite or S to skip: ");
        }
    }
}
