using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GMinor.Wpf.Views;

namespace GMinor.Wpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DispatchPage _dispatchPage = new();
    private readonly HistoryPage  _historyPage  = new();
    private readonly SettingsPage _settingsPage = new();

    [ObservableProperty]
    private object _currentPage;

    public MainViewModel()
    {
        _currentPage = _dispatchPage;
    }

    [RelayCommand]
    void Navigate(string destination)
    {
        CurrentPage = destination switch
        {
            "History"  => (object)_historyPage,
            "Settings" => _settingsPage,
            _          => _dispatchPage,
        };
    }
}
