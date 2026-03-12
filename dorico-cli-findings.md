# Dorico CLI Tool — Research Findings & Design Notes

**Date:** 2026-03-12
**Context:** This document captures everything discovered during a review of a Dorico MCP server
project, plus architectural thinking for building a standalone CLI tool that controls Dorico.
Intended to onboard a follow-up agent or developer without requiring them to re-research the basics.

---

## 1. How Dorico Exposes Remote Control

Dorico (v4+) ships with a built-in **HTTP-based Remote Control API** that listens on
`localhost:4000` by default. It must be enabled in Dorico's preferences
(**General → Advanced → Enable Remote Control API**). Once on, Dorico accepts HTTP GET requests
shaped like RPC calls.

### Request format

```
GET http://localhost:4000/<Namespace>.<Command>?Param=Value&Param2=Value2
```

Responses are JSON. The top-level shape is typically:
```json
{ "result": "OK" }
// or
{ "result": "ERROR", "message": "..." }
// or a data payload for query-style commands
```

No authentication. No API key. Localhost only (not exposed on the network by default).

---

## 2. Confirmed Commands (discovered during review)

### File operations
| Command | Params | Notes |
|---------|--------|-------|
| `File.Export` | `Format=PDF`, `Path=<abs_path>` | Basic export only — no page range, layout selector, or print options confirmed |
| `File.Open` | `Path=<abs_path>` | Opens a project |
| `File.Save` | — | Saves current project |
| `File.Close` | — | Closes current project |

### Edit operations
| Command | Params | Notes |
|---------|--------|-------|
| `Edit.Transpose` | `Semitones=<-48..48>` | Chromatic transposition of current selection |
| `Edit.TransposeOctave` | `Direction=Up` or `Direction=Down` | Octave shift of current selection |

### Options / layout queries
| Command | Notes |
|---------|-------|
| `get_engraving_options` | Returns Dorico's engraving settings as JSON |
| `set_engraving_options` | Bulk-sets engraving settings |
| `get_layout_options` | Returns layout settings |
| `set_layout_options` | Bulk-sets layout settings |
| `get_notation_options` | Returns notation/playback options |
| `set_notation_options` | Bulk-sets notation options |

---

## 3. Confirmed Gaps / Unknowns

These are areas where the review was **inconclusive** — commands may exist but were not verified:

| Gap | Impact | How to resolve |
|-----|--------|----------------|
| `Edit.SelectAll` (or equivalent) | Can't do whole-score transpose without it | Log-sniff Dorico while pressing Cmd+A; check forum threads |
| PDF export options (layout, page range, crop marks) | Export is all-or-nothing right now | Try undocumented params like `?Format=PDF&Layout=FullScore&PageRange=1-4`; sniff the network tab in Dorico's remote control debugger if one exists |
| Diatonic transposition | `Edit.Transpose` is chromatic only; no "up a third in key" command found | May not exist — could require Select All + Transpose + manual key change workaround |
| `get_score_info` / metadata queries | No confirmed way to read back title, composer, key, time sig programmatically | Sniff or check the Dorico Lua/scripting forum |
| `Playback.Play` / `Playback.Stop` | Not confirmed | Low priority but useful for auditioning |
| Multi-layout / part export | Export a full score + all parts in one call | Not confirmed; may require iterating layouts |

---

## 4. Local (Non-Dorico) Capabilities from the MCP Server

The reviewed MCP server also bundled **music21** as a local analysis engine. These tools run
entirely in Python and do **not** communicate with Dorico — they analyze/transform data in memory:

- `check_species_rules` — counterpoint rule validation (1st–5th species)
- `transpose_for_instrument` — sounding-to-written pitch conversion for transposing instruments
- `set_key_signature` — key sig manipulation on a music21 stream

For a CLI tool, these could either be:
- Kept as a local Python subprocess call
- Reimplemented in C# using a library like **Melanchall.DryWetMidi** or **NStack** (no direct
  music21 .NET equivalent, but MIDI-level transforms are doable)
- Dropped if the use case is purely Dorico automation

---

## 5. Recommended CLI Architecture

### Technology choice

Given that GMinor is already a **.NET 10** project targeting Windows, the CLI tool should be:

- **.NET 10 console app** (fits the existing solution)
- Could live as `GMinor.DoricoClient` — a new project in `GMinor.slnx`
- HTTP calls via `HttpClient` (built-in, no extra deps needed)

### Proposed structure

```
GMinor.DoricoClient/
  DoricoClient.cs          # thin HttpClient wrapper — raw command dispatch
  Commands/
    FileCommands.cs        # Open, Save, Close, Export
    EditCommands.cs        # Transpose, SelectAll (once confirmed)
    OptionsCommands.cs     # get/set engraving/layout/notation options
  DoricoCliApp.cs          # System.CommandLine entrypoint
  Program.cs
```

### DoricoClient core

```csharp
public class DoricoClient(string host = "localhost", int port = 4000)
{
    private readonly HttpClient _http = new();
    private readonly string _base = $"http://{host}:{port}";

    public async Task<JsonDocument> SendAsync(string ns, string command,
        IReadOnlyDictionary<string, string>? @params = null)
    {
        var query = @params is { Count: > 0 }
            ? "?" + string.Join("&", @params.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"))
            : "";
        var url = $"{_base}/{ns}.{command}{query}";
        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }
}
```

### CLI surface (System.CommandLine)

```
dorico export pdf --path C:\out.pdf
dorico transpose --semitones 3
dorico transpose --octave up
dorico options get engraving
dorico options set engraving --json @opts.json
dorico open --path C:\scores\MySong.dorico
dorico save
```

### Connection check / health

Before any command, ping Dorico:
```
GET http://localhost:4000/
```
If it times out or returns a non-2xx, surface a clear error:
> "Dorico is not running or Remote Control API is not enabled.
>  Enable it at: General → Advanced → Enable Remote Control API"

---

## 6. Whole-Score Transpose Workflow (Current Best Guess)

Until `SelectAll` is confirmed, the safest approach is:

1. Prompt user to manually select all in Dorico, OR
2. Try `Edit.SelectAll` and handle failure gracefully
3. Call `Edit.Transpose?Semitones=N`
4. For key-aware transposition: also call `set_notation_options` to update key signature
   (exact param name TBD — needs research)

---

## 7. PDF Export — Known Limitation & Workaround Ideas

`File.Export?Format=PDF&Path=X` appears to export whatever Dorico's current default export
settings are. Options to work around the lack of exposed params:

1. **Pre-set via `set_layout_options`** before exporting — configure what you need in Dorico's
   data model, then export. This is the most likely intended workflow.
2. **Iterate layouts**: if Dorico has a `get_layouts` command (unconfirmed), you could loop over
   layout IDs and export each separately.
3. **Log sniffing**: run Dorico, open DevTools or a proxy like Fiddler pointed at localhost,
   then manually trigger Print → Save as PDF to see what params Dorico's own UI sends.

---

## 8. Immediate Next Steps for the Implementing Agent

1. **Verify `SelectAll`**: fire `GET http://localhost:4000/Edit.SelectAll` at a running Dorico
   instance and record the response. This unblocks whole-score transpose.

2. **Sniff PDF export params**: use a local MITM proxy or Dorico's own log output
   (`%APPDATA%\Steinberg\Dorico 5\dorico5.log`) while triggering a print-to-PDF to find any
   hidden params.

3. **Check Steinberg's developer forum** at https://forums.steinberg.net/c/dorico — search
   "remote control API" for community-discovered undocumented commands.

4. **Scaffold the .NET project**: add `GMinor.DoricoClient` to `GMinor.slnx`, wire up
   `System.CommandLine` (already a stable NuGet package), implement `DoricoClient.cs`.

5. **Write integration tests** using `WireMock.Net` to mock the Dorico HTTP endpoint — mirrors
   the existing GMinor test pattern (real I/O for integration, mocked for unit).

---

## 9. Key Reference Links

- Steinberg Remote Control API docs (if public): check https://forums.steinberg.net
- System.CommandLine NuGet: `System.CommandLine` v2.x (stable as of 2025)
- WireMock.Net: `WireMock.Net` NuGet — for mocking the HTTP endpoint in tests
- Melanchall.DryWetMidi: if local MIDI/pitch analysis is needed in .NET

---

## Summary Table

| Capability | Status | Confidence |
|-----------|--------|------------|
| Open/Save/Close file | Confirmed | High |
| Export to PDF (basic) | Confirmed | High |
| Export to PDF (with options) | Unknown | Low — needs sniffing |
| Chromatic transpose (selection) | Confirmed | High |
| Octave transpose (selection) | Confirmed | High |
| Diatonic transpose | Not found | Low |
| Select All | Unconfirmed | Medium — likely exists |
| Whole-score transpose | Unconfirmed (depends on SelectAll) | Medium |
| Read score metadata (title, key, etc.) | Not found | Low |
| get/set engraving options | Confirmed | High |
| get/set layout options | Confirmed | High |
| get/set notation options | Confirmed | High |
| Playback control | Not found | Unknown |
