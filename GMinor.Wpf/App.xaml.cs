using System.Windows;

namespace GMinor.Wpf;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            MessageBox.Show(e.Exception.ToString(), "GMinor — Unhandled Exception",
                MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            MessageBox.Show(e.ExceptionObject?.ToString(), "GMinor — Fatal Exception",
                MessageBoxButton.OK, MessageBoxImage.Error);
        };
    }
}
