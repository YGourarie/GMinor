using System.Windows;
using GMinor.Core;

namespace GMinor.Wpf.Services;

public class MessageBoxConflictResolver : IConflictResolver
{
    public ConflictResolution Resolve(string sourcePath, string destinationPath)
    {
        var result = MessageBox.Show(
            $"A file already exists at:\n{destinationPath}\n\nOverwrite it?",
            "File Conflict",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return result == MessageBoxResult.Yes
            ? ConflictResolution.Overwrite
            : ConflictResolution.Skip;
    }
}
