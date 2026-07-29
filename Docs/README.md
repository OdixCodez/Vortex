# Vortex Shell

An object-oriented, cross-platform command shell built in native C# and .NET 9. Vortex Shell is a structured pipeline engine with neon-cyan branding, a fully custom REPL keystroke interceptor, automatic Windows Terminal integration, and complete PowerShell Verb-Noun cmdlet alias binding — all in a single, zero-dependency `Program.cs`.

---

## Repository Layout

```
Vortex/
├── Program.cs               # Complete shell — all modules, single file
├── VortexShell.csproj       # .NET project file
├── Docs/
│   ├── README.md            # This file
│   └── ARCHITECTURE.md      # Object pipeline design reference
├── .gitignore
└── LICENSE
```

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8) or [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9) on your **build machine**
- No runtime dependencies on target machines — every publish target below is fully self-contained

---

## Building from Source

```bash
git clone https://github.com/OdixCodez/Vortex.git
cd Vortex
dotnet restore
dotnet build
dotnet run
```

---

## Compilation — Standalone Native Binaries

Each command below produces a completely independent single-file native executable bundling the .NET runtime, all managed assemblies, and all native libraries. Target machines require **zero pre-installed .NET dependencies**.

### Windows 10 / 11 — x64

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:IncludeNativeLibrariesForSelfExtracted=true
```

Output: `bin/Release/net9.0/win-x64/publish/vortex.exe`

### Windows 11 — ARM64 (Surface Pro X, Copilot+ PCs)

```bash
dotnet publish -c Release -r win-arm64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true
```

Output: `bin/Release/net9.0/win-arm64/publish/vortex.exe`

### macOS — Apple Silicon (M1 / M2 / M3 / M4)

```bash
dotnet publish -c Release -r osx-arm64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtracted=true
```

Output: `bin/Release/net9.0/osx-arm64/publish/vortex`

```bash
chmod +x vortex && ./vortex
```

### macOS — Intel

```bash
dotnet publish -c Release -r osx-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtracted=true
```

Output: `bin/Release/net9.0/osx-x64/publish/vortex`

```bash
chmod +x vortex && ./vortex
```

### Linux — x64 (Ubuntu, Debian, Fedora, Alpine)

```bash
dotnet publish -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtracted=true
```

Output: `bin/Release/net9.0/linux-x64/publish/vortex`

```bash
chmod +x vortex && ./vortex
```

### Linux — ARM64 (Raspberry Pi 4+, AWS Graviton)

```bash
dotnet publish -c Release -r linux-arm64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtracted=true
```

```bash
chmod +x vortex && ./vortex
```

> **Note on `chmod +x`:** On all Unix-based systems, downloaded or copied binaries do not carry the executable bit by default. Without this step the shell returns `Permission denied` even if the binary is valid.

---

## Running Vortex Shell

```bash
# Windows
.\vortex.exe

# macOS / Linux
./vortex

# Version info
./vortex --version

# One-shot non-interactive command
./vortex -c "ls /etc"

# Force Windows Terminal profile re-deployment
.\vortex.exe --deploy-terminal
```

---

## Command Reference

All commands are case-insensitive. Vortex Shell supports classic Unix short aliases, formal PowerShell Verb-Noun cmdlets, and official PowerShell short forms simultaneously.

### Navigation

| Alias | PowerShell Cmdlet | Short Form | Description |
|---|---|---|---|
| `pwd` | `Get-Location` | `gl` | Print current working directory |
| `cd <path>` | `Set-Location <path>` | `sl` | Change directory (quoted paths with spaces supported) |
| `ls [path]` | `Get-ChildItem [path]` | `gci`, `dir` | List directory as structured object table |

### File Operations

| Alias | PowerShell Cmdlet | Short Form | Description |
|---|---|---|---|
| `cat <file>` | `Get-Content <file>` | `gc`, `type` | Read file line by line |
| `mkdir <path>` | `New-Item <path>` | `md` | Create a directory |
| `rm <path>` | `Remove-Item <path>` | `del`, `ri` | Remove file or directory |
| `rm -r <path>` | `Remove-Item -Recurse <path>` | | Recursive directory removal |
| `cp <src> <dst>` | `Copy-Item <src> <dst>` | `copy`, `ci` | Copy a file |
| `mv <src> <dst>` | `Move-Item <src> <dst>` | `move`, `mi` | Move or rename a file |
| `measure <path>` | `Measure-Object <path>` | | File/dir stats: lines, words, chars, size |

### Process Management

| Alias | PowerShell Cmdlet | Short Form | Description |
|---|---|---|---|
| `ps` | `Get-Process` | `gps` | Snapshot of top 20 processes by memory |
| `ps <name>` | `Get-Process <name>` | | Filter processes by name |
| `vortex-top` | | | Live process monitor, refreshes every 1s (Q / Esc to exit) |

### Variables & Environment

| Alias | PowerShell Cmdlet | Short Form | Description |
|---|---|---|---|
| `$name = value` | `Set-Variable name value` | `sv` | Set a session variable |
| `$name` | `Get-Variable name` | `gv` | Read a variable |
| `echo $name` | `Write-Host $name` | `Write-Output` | Expand and print |
| `env` | `Get-ChildItem env:` | | List all OS environment variables |

### Shell Control

| Alias | PowerShell Cmdlet | Description |
|---|---|---|
| `clear` | `cls`, `Clear-Host` | Clear the terminal screen |
| `exit` | `quit` | Exit Vortex Shell |
| `help` | `Get-Help`, `man` | Print the full command reference |
| `history` | `Get-History`, `h` | Show commands entered this session |
| `--version` | `version`, `-v` | Print version and runtime metadata |

### Vortex Special Commands

| Command | Description |
|---|---|
| `vortex-matrix` | Multi-column digital rain animation (Space / Esc to exit) |
| `vortex-sys` | System diagnostics: OS, arch, runtime, CPU threads, uptime, GC heap, visual load bars |
| `vortex-top` | Live task manager — top 10 processes by memory, refreshes every 1s (Q / Esc to exit) |

Any command not matched by the built-in router is forwarded to the native OS shell — `cmd.exe /c` on Windows, `/bin/sh -c` on macOS and Linux.

---

## Session Variables

```
vortex > $project = my-awesome-app
  $project = "my-awesome-app"

vortex > cd $project
vortex > ls

vortex > $host = 192.168.1.100
vortex > echo $host
192.168.1.100
```

Variables persist for the lifetime of the session and are never written to disk.

---

## Tab Completion

Press `Tab` while typing a path argument to complete the first matching filesystem entry in the current directory. Entries containing spaces are automatically wrapped in double quotes.

---

## Windows Terminal Auto-Deployment

On first launch on a Windows host, Vortex Shell automatically deploys a Windows Terminal Fragment profile to:

```
%LOCALAPPDATA%\Microsoft\Windows Terminal\Fragments\VortexShell\vortex-profile.json
```

This permanently registers a **Vortex Shell** entry in Windows Terminal's profile list with:

- **Font**: Cascadia Code with full ligature support
- **Cursor**: `filledBox` shape, color `#00FFFF`
- **Background**: `#0A0E14`
- **Acrylic**: 65% opacity blur glass layer
- **Color scheme**: VortexCyan (full 16-color terminal palette)

Re-run `vortex.exe --deploy-terminal` to refresh the profile after updating the binary.

---

## No External Dependencies

Vortex Shell has zero NuGet package dependencies. The entire implementation compiles against only `System.*` namespaces from the .NET Base Class Library. This eliminates supply-chain risk and makes every published binary fully auditable from a single source file.
