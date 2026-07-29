# Vortex Shell

An object-oriented, cross-platform command shell built in C# and .NET 8 — a direct competitor to PowerShell with a structured pipeline engine, neon-cyan branding, and a fully custom REPL.

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) installed on your **build machine**
- No runtime dependencies required on **target machines** — all binaries below are fully self-contained

---

## Project Structure

```
vortex-shell/
├── src/
│   ├── Program.cs
│   └── VortexShell.csproj
├── docs/
│   ├── README.md
│   └── ARCHITECTURE.md
└── bin/
    └── (compiled output artifacts)
```

---

## Building from Source

Clone or download the repository, then navigate into the `src/` directory before running any publish command.

```bash
cd src/
```

---

## Compilation — Standalone Native Binaries

Each command below produces a **completely independent, single-file native executable** that bundles the .NET runtime, all managed assemblies, and all native platform libraries into one artifact. The target machine requires **zero pre-installed .NET dependencies**.

### Windows 10 / 11 (x64)

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:IncludeNativeLibrariesForSelfContained=true
```

Output: `bin/Release/net8.0/win-x64/publish/vortex.exe`

---

### macOS — Apple Silicon (M1 / M2 / M3 / M4 / M5)

```bash
dotnet publish -c Release -r osx-arm64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfContained=true
```

Output: `bin/Release/net8.0/osx-arm64/publish/vortex`

After copying the binary to the target machine, grant execution rights:

```bash
chmod +x vortex
./vortex
```

---

### macOS — Legacy Intel Chipsets (x64)

```bash
dotnet publish -c Release -r osx-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfContained=true
```

Output: `bin/Release/net8.0/osx-x64/publish/vortex`

After copying the binary to the target machine, grant execution rights:

```bash
chmod +x vortex
./vortex
```

---

### Linux Distributions — 64-bit Server / Desktop

```bash
dotnet publish -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfContained=true
```

Output: `bin/Release/net8.0/linux-x64/publish/vortex`

After copying the binary to the target machine, grant execution rights:

```bash
chmod +x vortex
./vortex
```

> **Note on `chmod +x`:** On all Unix-based systems (macOS and Linux), downloaded or copied binaries do not carry the executable bit by default. Running `chmod +x <binary>` sets the execution permission flag, allowing the OS to run the file directly. Without this step, the shell will return a `Permission denied` error even if the binary is valid.

---

## Running Vortex Shell

```bash
# Windows
.\vortex.exe

# macOS / Linux
./vortex
```

---

## Built-in Commands

| Command | Alias | Description |
|---|---|---|
| `pwd` | `Get-Location` | Print working directory |
| `cd <path>` | `Set-Location` | Change directory (supports quoted paths with spaces) |
| `ls` | `dir`, `Get-ChildItem` | List directory contents in a structured table |
| `vortex-matrix` | — | Falling digital rain animation (Spacebar or Esc to exit) |
| `vortex-sys` | — | System diagnostics: OS, CPU, GC metrics with visual bars |
| `vortex-top` | — | Live process monitor sorted by memory, refreshes every 1s |
| `history` | — | Show all commands entered this session |
| `clear` | `cls` | Clear the terminal |
| `exit` | `quit` | Exit Vortex Shell |
| `$name = value` | — | Set a session-level string variable |

Any command not matched by the built-in router is forwarded transparently to the underlying OS shell (`cmd.exe /c` on Windows, `/bin/sh -c` on macOS and Linux).

---

## Session Variables

Vortex Shell supports simple session-scoped string variables using `$` prefix notation:

```
vortex > $project = my-app
vortex > cd $project
vortex > ls
```

Variables are stored in memory for the lifetime of the session and are not persisted between runs.

---

## Version Flag

```bash
./vortex --version
# Vortex Shell v1.0.0 (.NET 8)
```

---

## No External Dependencies

Vortex Shell has **zero NuGet package dependencies**. The entire implementation compiles against the .NET 8 Base Class Library only. This eliminates supply-chain risk and makes the published binary fully auditable from a single source file.
