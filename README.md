# GMinor

Automated file routing for .NET 10. Reads a source folder and moves each file to the correct destination based on a single hand-authored C# function — no DSL, no config, no abstractions.

---

## Quick Start

```bash
# Build
dotnet build

# Run tests
dotnet test

# Route files (reads SourceFolder from appsettings.json)
dotnet run --project GMinor.Console

# Preview without moving anything
dotnet run --project GMinor.Console -- --dry-run
```

---

## Deploying to Windows

Run the publish script from WSL:

```bash
~/Projects/GMinor/publish.sh
```

This publishes a self-contained Windows executable directly to `C:\Tools\GMinor` — no .NET installation required on the Windows side, no manual copying needed.

Then from a Windows terminal:

```cmd
cd C:\Tools\GMinor

# Route files for real
GMinor.Console.exe

# Preview without moving anything
GMinor.Console.exe --dry-run
```

Re-run `publish.sh` whenever you update routing rules or any other code — it overwrites the existing files in place.

---

## Project Layout

```
GMinor.Core/                  ← Class library — all logic lives here
│   FileDispatcher.cs         ← Dispatcher + conflict types + exceptions
│   RoutingResult.cs          ← Output of a routing decision
│   DispatchResult.cs         ← Outcome of a dispatch call
│   Rules/
│       RoutingRules.cs       ← ★ THE one place to add routing logic

GMinor.Console/               ← One-shot console host (~60 lines)
│   Program.cs                ← Parses args, enumerates folder, calls Dispatch
│   InteractiveConflictResolver.cs  ← Prompts O/S at the terminal
│   appsettings.json          ← Set SourceFolder here

GMinor.Tests.Unit/            ← Fast, no-I/O unit tests (17 tests)
│   RoutingRulesTests.cs      ← Tests the routing function directly
│   FileDispatcherTests.cs    ← Tests dispatcher with mock routing + resolvers

GMinor.Tests.Integration/     ← Real filesystem tests using temp dirs (9 tests)
    FileDispatcherIntegrationTests.cs
```

---

## Adding a Routing Rule

Open **`GMinor.Core/Rules/RoutingRules.cs`** — that is the only file you ever need to edit for new routing logic.

Add a `Regex.Match` block before the final `return RoutingResult.NoMatch`:

```csharp
public static RoutingResult Route(string filename)
{
    // Existing rule: "2024-03-15_invoice_0042.pdf" → Invoices\2024\invoice_0042.pdf
    var m = Regex.Match(filename,
        @"^(?<date>\d{4}-\d{2}-\d{2})_invoice_(?<num>\d+)\.pdf$",
        RegexOptions.IgnoreCase);

    if (m.Success)
        return new RoutingResult
        {
            DestDir  = $@"C:\Sorted\Invoices\{m.Groups["date"].Value[..4]}",
            DestName = $"invoice_{m.Groups["num"].Value}.pdf",
        };

    // ↓ Add new rules here ↓

    // Example: move any PNG to a Screenshots folder, keep original name
    // if (filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
    //     return new RoutingResult { DestDir = @"C:\Sorted\Screenshots" };

    return RoutingResult.NoMatch;  // ← always last
}
```

- Return only `DestDir` to preserve the original filename.
- Return both `DestDir` and `DestName` to rename the file at the destination.
- Rules are evaluated top-to-bottom; the first match wins.
- All patterns should use `RegexOptions.IgnoreCase`.

---

## Configuration

**`GMinor.Console/appsettings.json`**

```json
{
  "FileRouter": {
    "SourceFolder": "C:\\Drop"
  }
}
```

Only top-level files in `SourceFolder` are processed (no subdirectories).

---

## Core API

```csharp
// Construct with any routing function
var dispatcher = new FileDispatcher(RoutingRules.Route);

// Dispatch a single file
DispatchResult result = dispatcher.Dispatch(
    filePath:  @"C:\Drop\2024-03-15_invoice_0042.pdf",
    dryRun:    false,
    resolver:  null   // or an IConflictResolver implementation
);
```

`DispatchOutcome` values: `Moved` | `Skipped` | `DryRun` | `Overwritten`

### Conflict resolution

When a file already exists at the destination:

| Scenario | Behavior |
|----------|----------|
| `resolver` provided, returns `Overwrite` | Destination replaced, outcome `Overwritten` |
| `resolver` provided, returns `Skip` | Source left in place, outcome `Skipped` |
| `resolver` is `null` | `ConflictException` thrown |

The console host uses `InteractiveConflictResolver` (keyboard prompt). Provide your own `IConflictResolver` for any other host (service, GUI, policy-based).

---

## Logging

The console app writes structured logs to two sinks:

- **Terminal** — colored output via Serilog console sink
- **Rolling file** — `logs/gminor-YYYYMMDD.log` next to the executable

To wire the library's internal logger in another host:

```csharp
var dispatcher = new FileDispatcher(RoutingRules.Route, loggerFactory.CreateLogger<FileDispatcher>());
```

If no logger is provided, a no-op logger is used.

---

## Future Hosts

The library has no coupling to any host. Any future host follows the same pattern:

```csharp
var dispatcher = new FileDispatcher(RoutingRules.Route);

// FSW daemon
watcher.Created += (_, e) => dispatcher.Dispatch(e.FullPath, resolver: myResolver);

// Polling loop
foreach (var file in Directory.EnumerateFiles(source))
    dispatcher.Dispatch(file, resolver: myResolver);

// GUI preview
var result = dispatcher.Dispatch(file, dryRun: true);
```
