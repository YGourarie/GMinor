using System.Windows.Threading;
using GMinor.Core;

namespace GMinor.Wpf.Services;

public class DispatcherConflictResolver : IConflictResolver
{
    private readonly IConflictResolver _inner;
    private readonly Dispatcher _dispatcher;

    public DispatcherConflictResolver(IConflictResolver inner, Dispatcher dispatcher)
    {
        _inner = inner;
        _dispatcher = dispatcher;
    }

    public ConflictResolution Resolve(string sourcePath, string destinationPath)
        => _dispatcher.Invoke(() => _inner.Resolve(sourcePath, destinationPath));
}
