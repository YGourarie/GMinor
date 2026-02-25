# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build
dotnet build -c Release

# Test
dotnet test                          # all tests
dotnet test GMinor.Tests.Unit        # unit tests only
dotnet test GMinor.Tests.Integration # integration tests only
dotnet test --filter "FullyQualifiedName~MethodName"  # single test

# Publish Windows self-contained exe (from WSL)
./publish.sh                  # runs tests, then publishes both Console and Wpf
./publish.sh --console        # publish GMinor.Console only
./publish.sh --gui            # publish GMinor.Wpf only
./publish.sh --skip-tests     # skip tests before publishing
```

## Architecture

GMinor is a .NET 10 file routing tool. The design principle is: **no DSL, no config for routing logic—just a C# function.**

### Key components

**`GMinor.Core/FileDispatcher.cs`** — the core engine. Contains:
- `FileDispatcher` class: takes `Func<string, RoutingResult> routingFunc` via constructor (DI pattern enabling testability)
- `IConflictResolver` interface: for handling destination conflicts (skip/overwrite)
- `ConflictException` and `FileLockedException` exception types
- `Dispatch(filePath, dryRun, resolver)`: orchestrates routing lookup → conflict check → file move

**`GMinor.Core/Rules/RoutingRules.cs`** — the ONLY file users edit to change routing behavior. Single static method `Route(filename, destRoot)` returns a `RoutingResult`.

**`GMinor.Core/RoutingResult.cs`** — record type: `DestDir` (required for match) + `DestName` (optional rename). `IsMatch` returns true when `DestDir != null`. `RoutingResult.NoMatch` is the sentinel.

**`GMinor.Core/DispatchResult.cs`** — record type capturing outcome: `Moved`, `Skipped`, `DryRun`, or `Overwritten`.

**`GMinor.Console/Program.cs`** — host: loads `appsettings.json`, enumerates top-level files in `SourceFolder`, calls `Dispatch()` per file, wires Serilog logging.

**`GMinor.Wpf/`** — WPF GUI host. Uses CommunityToolkit.Mvvm (MVVM). `DispatchViewModel` handles folder selection, dry-run toggle, and async dispatch. `SettingsService` persists source/destination paths to `appsettings.json`. `MessageBoxConflictResolver` + `DispatcherConflictResolver` handle conflicts via MessageBox on the UI thread. History and Settings pages are stubs.

### Data flow

```
appsettings.json → SourceFolder/DestinationFolder
→ Directory.EnumerateFiles(source, TopDirectoryOnly)
→ FileDispatcher.Dispatch(filePath, dryRun, resolver)
  → RoutingRules.Route(filename, destRoot) → RoutingResult
  → conflict check → IConflictResolver
  → File.Move(source, dest)
```

### Testing approach

- **Unit tests** (`GMinor.Tests.Unit`): mock the routing function and `IConflictResolver` via Moq; no real I/O for dispatcher tests
- **Integration tests** (`GMinor.Tests.Integration`): use real temp directories, cleaned up via `IDisposable`; Windows file-locking tests are skipped on non-Windows

### Extension points

- **New routing rules**: add pattern matching in `RoutingRules.cs` before `return RoutingResult.NoMatch`
- **Custom conflict handling**: implement `IConflictResolver` and pass to `Dispatch()`
- **Alternative hosts**: `FileDispatcher` can be hosted in a `FileSystemWatcher` daemon, scheduled task, or GUI app—just inject a routing function

### Configuration

`GMinor.Console/appsettings.json`:
```json
{
  "FileRouter": {
    "SourceFolder": "C:\\Drop",
    "DestinationFolder": "C:\\Sorted"
  }
}
```

`publish.sh` preserves existing `appsettings.json` during publish to avoid overwriting user config.

### Solution file

Uses the newer `.slnx` format (`GMinor.slnx`).
