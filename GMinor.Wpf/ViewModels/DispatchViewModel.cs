using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GMinor.Core;
using GMinor.Core.Rules;
using GMinor.Wpf.Services;
using Microsoft.Win32;

namespace GMinor.Wpf.ViewModels;

public partial class DispatchViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OrganizeCommand))]
    private string _sourcePath = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OrganizeCommand))]
    private string _destinationPath = "";

    [ObservableProperty]
    private bool _dryRun;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OrganizeCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasResults;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private int _movedCount;

    [ObservableProperty]
    private int _skippedCount;

    [ObservableProperty]
    private int _overwrittenCount;

    [ObservableProperty]
    private int _dryRunCount;

    [ObservableProperty]
    private int _errorCount;

    public DispatchViewModel()
    {
        _settingsService = new SettingsService();
        var settings = _settingsService.Load();
        _sourcePath      = settings.FileRouter.SourceFolder;
        _destinationPath = settings.FileRouter.DestinationFolder;
    }

    partial void OnSourcePathChanged(string value)
        => _settingsService.Save(SourcePath, DestinationPath);

    partial void OnDestinationPathChanged(string value)
        => _settingsService.Save(SourcePath, DestinationPath);

    [RelayCommand]
    void BrowseSource()
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog() == true)
            SourcePath = dialog.FolderName;
    }

    [RelayCommand]
    void BrowseDestination()
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog() == true)
            DestinationPath = dialog.FolderName;
    }

    private bool CanOrganize()
        => !string.IsNullOrWhiteSpace(SourcePath)
        && !string.IsNullOrWhiteSpace(DestinationPath)
        && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanOrganize))]
    async Task OrganizeAsync()
    {
        IsBusy     = true;
        HasResults = false;
        MovedCount = SkippedCount = OverwrittenCount = DryRunCount = ErrorCount = 0;
        StatusMessage = "";

        var sourcePath = SourcePath;
        var destPath   = DestinationPath;
        var dryRun     = DryRun;
        var dispatcher = Application.Current.Dispatcher;

        try
        {
            var resolver       = new DispatcherConflictResolver(new MessageBoxConflictResolver(), dispatcher);
            var fileDispatcher = new FileDispatcher(f => RoutingRules.Route(f, destPath));

            int moved = 0, skipped = 0, overwritten = 0, dryRunCount = 0, errors = 0;

            await Task.Run(() =>
            {
                foreach (var filePath in Directory.EnumerateFiles(sourcePath, "*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var result = fileDispatcher.Dispatch(filePath, dryRun, resolver);
                        switch (result.Outcome)
                        {
                            case DispatchOutcome.Moved:       moved++;       break;
                            case DispatchOutcome.Skipped:     skipped++;     break;
                            case DispatchOutcome.Overwritten: overwritten++; break;
                            case DispatchOutcome.DryRun:      dryRunCount++; break;
                        }
                    }
                    catch (FileLockedException)
                    {
                        errors++;
                    }
                }
            });

            MovedCount       = moved;
            SkippedCount     = skipped;
            OverwrittenCount = overwritten;
            DryRunCount      = dryRunCount;
            ErrorCount       = errors;

            StatusMessage = dryRun
                ? $"Dry run complete — {dryRunCount} file(s) would be moved."
                : $"Done. {moved} moved, {skipped} skipped, {overwritten} overwritten, {errors} error(s).";
            HasResults = true;
        }
        catch (DirectoryNotFoundException ex)
        {
            StatusMessage = $"Directory not found: {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
