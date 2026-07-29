# Vortex Shell — Architecture Reference

This document describes the internal design of Vortex Shell: its object model, pipeline engine, REPL mechanics, and cross-platform execution routing.

---

## Repository Layout

```
vortex-shell/
│
├── src/
│   ├── Program.cs              # Complete shell implementation (single file)
│   └── VortexShell.csproj      # .NET 8 project file
│
├── docs/
│   ├── README.md               # Build instructions and command reference
│   └── ARCHITECTURE.md         # This file
│
└── bin/
    └── Release/
        └── net8.0/
            ├── win-x64/publish/vortex.exe
            ├── osx-arm64/publish/vortex
            ├── osx-x64/publish/vortex
            └── linux-x64/publish/vortex
```

---

## Class Map

```
VortexShell (namespace)
│
├── VortexObject                  Core structured data unit
├── PipelineResult                Discriminated output container
├── Renderer                      Table and raw output formatter
├── SessionState                  Variable store and history list
├── InputReader                   Custom keystroke REPL engine
├── BuiltIns                      Native command implementations
├── ExternalRunner                OS shell subprocess bridge
├── Evaluator                     Command router and tokenizer
├── Shell                         Banner, prompt, main REPL loop
└── Program                       Entry point and version flag
```

---

## Object-Oriented Pipeline Paradigm

### The Problem with Text Pipelines

Traditional Unix shells — and even early PowerShell drafts — transport data between commands as **unstructured byte streams**. Every consumer must re-parse the same text to extract structure: column widths, delimiters, and field names are implicit and fragile. A change in output formatting anywhere in a chain silently breaks downstream consumers.

### Vortex Pipeline Design

Vortex Shell replaces byte streams with a typed object graph. Every built-in command returns a `PipelineResult` which carries either:

- A `List<VortexObject>` — structured records with named property bags
- A `List<string>` — raw lines for commands that produce unstructured output (external process output, help text)

```
Command Executes
       │
       ▼
  PipelineResult
  ┌──────────────────────────────┐
  │  IsRaw: false                │    ← Object path
  │  Objects: List<VortexObject> │
  │    ├─ { Type, Name, Size }   │
  │    └─ { Type, Name, Size }   │
  └──────────────────────────────┘
          OR
  ┌──────────────────────────────┐
  │  IsRaw: true                 │    ← Raw text path
  │  RawLines: List<string>      │
  └──────────────────────────────┘
       │
       ▼
   Renderer
   Detects path → auto-formats aligned table OR prints raw lines
```

### VortexObject

`VortexObject` is the atomic unit of data. It holds a `Dictionary<string, object>` of named properties with no fixed schema — each command defines its own property set.

```csharp
var entry = new VortexObject(new Dictionary<string, object>
{
    ["Type"]     = "File",
    ["Name"]     = "Program.cs",
    ["Modified"] = "2025-07-29 14:30",
    ["Size"]     = "18.4 KB"
});
```

This design allows future commands to add new fields without breaking the renderer: column widths are computed dynamically across the full result set at render time.

### Renderer — Dynamic Table Alignment

When `IsRaw` is `false`, the `Renderer` class:

1. Collects the union of all property keys across all objects in the result set
2. Computes the max display width per column (header vs. max value length)
3. Renders a header row, a separator row, and one row per object — with per-object color overrides (e.g., directories in blue, files in gray)

Column widths self-adjust to content. No fixed format strings are hardcoded.

---

## REPL and Keystroke Engine

### Why Not `Console.ReadLine()`

`Console.ReadLine()` surrenders full control to the OS terminal driver. It cannot:

- Intercept arrow keys for history navigation
- Perform in-place line redraws during editing
- React to mid-input state (cursor position, insert mode)

### InputReader Design

`InputReader.ReadLine()` processes one `ConsoleKeyInfo` at a time via `Console.ReadKey(true)` (the `true` suppresses default echo). It maintains:

- A `StringBuilder` as the mutable line buffer
- An integer `cursorPos` tracking the logical insertion point within the buffer
- A `historyIndex` integer that walks `session.History` on Up/Down arrow

```
Key Event Flow:

ReadKey(true)
     │
     ├─ Enter       → return buffer.ToString()
     ├─ Backspace   → remove char at cursorPos-1, redraw
     ├─ Delete      → remove char at cursorPos, redraw
     ├─ LeftArrow   → decrement cursorPos, reposition cursor
     ├─ RightArrow  → increment cursorPos, reposition cursor
     ├─ Home        → cursorPos = 0
     ├─ End         → cursorPos = buffer.Length
     ├─ UpArrow     → historyIndex--, load history[historyIndex], redraw
     ├─ DownArrow   → historyIndex++, load history entry or "", redraw
     └─ Printable   → insert at cursorPos, redraw
```

### Line Redraw Strategy

`RedrawLine()` repositions the terminal cursor to the column immediately after the prompt, writes spaces to erase the old content across the full terminal width, repositions again, writes the new buffer content, then repositions to `promptLength + cursorPos`. This flush-and-repaint cycle is the same technique used by readline-compatible terminals.

---

## Command Router

`Evaluator.Evaluate()` is the central dispatch point. Input flows through these stages:

```
Raw Input String
       │
       ▼
  Variable Assignment Check ($name = value)
       │  no match
       ▼
  Variable Expansion (replace $name tokens with session values)
       │
       ▼
  Tokenizer (respects single and double quoted strings)
       │
       ▼
  Switch on cmd.ToLowerInvariant()
       │
       ├─ pwd / get-location     → BuiltIns.Pwd()
       ├─ cd / set-location      → BuiltIns.Cd(args)
       ├─ ls / dir / get-childitem → BuiltIns.Ls(args)
       ├─ vortex-sys             → BuiltIns.VortexSys()
       ├─ vortex-matrix          → BuiltIns.VortexMatrix()
       ├─ vortex-top             → BuiltIns.VortexTop()
       ├─ clear / cls            → Console.Clear()
       ├─ exit / quit            → Environment.Exit(0)
       ├─ help                   → static help table
       ├─ history                → session.History lines
       └─ (fallthrough)          → ExternalRunner.Run(input)
```

### Tokenizer

The tokenizer walks the input character-by-character, tracking whether a quote context is active. Text inside matching `"..."` or `'...'` is emitted as a single token regardless of whitespace. This enables:

```
cd "My Documents/Project Files"
```

to correctly produce a single path token rather than splitting on the space.

---

## Cross-Platform Execution Routing

### Platform Detection

`ExternalRunner.Run()` inspects the host OS at runtime using `System.Runtime.InteropServices.RuntimeInformation`:

```csharp
bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

string shell = isWindows ? "cmd.exe" : "/bin/sh";
string flag  = isWindows ? "/c"      : "-c";
```

This produces a subprocess invocation equivalent to:

- **Windows:** `cmd.exe /c <command>`
- **macOS / Linux:** `/bin/sh -c <command>`

### Subprocess Configuration

All external process launches use `ProcessStartInfo` with a fixed set of flags:

| Flag | Value | Reason |
|---|---|---|
| `UseShellExecute` | `false` | Required to redirect streams; disables OS shell lookup |
| `CreateNoWindow` | `true` | Prevents a second visible terminal window from spawning |
| `RedirectStandardOutput` | `true` | Captures stdout into managed memory |
| `RedirectStandardError` | `true` | Captures stderr separately for color-coded display |
| `StandardOutputEncoding` | `UTF8` | Ensures multi-byte characters survive stream capture |
| `StandardErrorEncoding` | `UTF8` | Same — prevents mojibake on localized OS error messages |

### Async Stream Capture

Output and error streams are read asynchronously using `BeginOutputReadLine()` and `BeginErrorReadLine()` with event handlers appending to `List<string>` buffers. `WaitForExit()` blocks synchronously until the subprocess terminates, ensuring the full output is collected before the result is returned to the renderer.

Stderr lines are flushed to the terminal immediately in yellow (`ConsoleColor.Yellow`) before the stdout result is returned, making error output visually distinct from command output.

---

## Specialty Commands

### `vortex-matrix`

Uses a non-blocking key poll loop (`Console.KeyAvailable`) rather than blocking on `Console.ReadKey()`. This allows the animation to run continuously on the main thread while remaining responsive to Spacebar or Escape interruption without requiring background threads. A single `cols[]` integer array tracks the vertical drop position of each screen column independently.

### `vortex-sys`

Reads all metrics synchronously from managed APIs:
- `Environment.ProcessorCount` — logical CPU thread count
- `GC.GetTotalMemory(false)` — current GC heap allocation without forcing collection
- `GC.CollectionCount(n)` — per-generation collection counters
- `RuntimeInformation.OSDescription / ProcessArchitecture / FrameworkDescription`

Visual load bars are rendered inline using Unicode block characters (`█` / `░`) with color thresholds at 40% (green → yellow) and 75% (yellow → red).

### `vortex-top`

Refreshes on a 1-second cycle. Within each cycle it:

1. Calls `Process.GetProcesses()` to snapshot all running processes
2. Sorts descending by `WorkingSet64` (private working set memory)
3. Takes the top 10 entries
4. Renders a full-terminal table with PID, name, memory, thread count, and response status

Processes consuming more than 500 MB are rendered in red. The inner loop polls `Console.KeyAvailable` every 50ms within the 1-second refresh window so Q or Escape is intercepted with minimal latency.

---

## Design Constraints

- **Zero external dependencies.** No NuGet packages. Only `System.*` namespaces from the .NET 8 BCL.
- **Single source file.** The entire shell is `Program.cs`. There are no partial classes, generated files, or build-time code generation steps.
- **No `Console.ReadLine()`.** All input is intercepted at the keystroke level.
- **Graceful exception isolation.** All command execution is wrapped at the REPL loop level; a crashing command prints the error message and returns to the prompt without terminating the shell process.
