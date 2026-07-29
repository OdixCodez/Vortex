using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace VortexShell
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  MODULE 1 — VORTEX OBJECT PIPELINE ENGINE
    // ═══════════════════════════════════════════════════════════════════════════

    sealed class VortexObject
    {
        public Dictionary<string, object> Properties { get; } = new();

        public VortexObject() { }

        public VortexObject(Dictionary<string, object> properties)
        {
            foreach (var kv in properties)
                Properties[kv.Key] = kv.Value;
        }

        public object? Get(string key) =>
            Properties.TryGetValue(key, out var v) ? v : null;

        public void Set(string key, object value) =>
            Properties[key] = value;
    }

    // ─── Pipeline Result Container ────────────────────────────────────────────

    sealed class PipelineResult
    {
        public List<VortexObject> Objects { get; } = new();
        public List<string> RawLines { get; } = new();
        public bool IsRaw { get; init; }

        public static PipelineResult FromObjects(IEnumerable<VortexObject> objects)
        {
            var r = new PipelineResult { IsRaw = false };
            r.Objects.AddRange(objects);
            return r;
        }

        public static PipelineResult FromRaw(IEnumerable<string> lines)
        {
            var r = new PipelineResult { IsRaw = true };
            r.RawLines.AddRange(lines);
            return r;
        }

        public static PipelineResult Empty() => new() { IsRaw = true };
    }

    // ─── Pipeline Renderer ────────────────────────────────────────────────────

    static class Renderer
    {
        public static void RenderResult(PipelineResult result)
        {
            if (result.IsRaw)
            {
                foreach (var line in result.RawLines)
                    Console.WriteLine(line);
                return;
            }

            if (result.Objects.Count == 0) return;

            var allKeys = result.Objects
                .SelectMany(o => o.Properties.Keys)
                .Distinct()
                .ToList();

            var colWidths = allKeys
                .Select(k => Math.Max(k.Length, result.Objects.Max(o =>
                    (o.Get(k)?.ToString() ?? "").Length)))
                .ToList();

            Console.WriteLine();
            RenderTableRow(allKeys, colWidths, ConsoleColor.Cyan);
            RenderSeparator(colWidths);

            foreach (var obj in result.Objects)
            {
                string typeVal = obj.Get("Type")?.ToString() ?? "";
                ConsoleColor rowColor = typeVal == "Directory"
                    ? ConsoleColor.Blue
                    : ConsoleColor.Gray;

                var values = allKeys.Select(k => obj.Get(k)?.ToString() ?? "").ToList();
                RenderColorizedRow(obj, allKeys, values, colWidths, rowColor);
            }

            Console.WriteLine();
        }

        static void RenderColorizedRow(
            VortexObject obj,
            List<string> keys,
            List<string> values,
            List<int> widths,
            ConsoleColor baseColor)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                string key = keys[i];
                string val = values[i];
                object? raw = obj.Get(key);

                ConsoleColor cellColor = baseColor;

                if (raw is long || raw is int || (long.TryParse(val, out _) && key != "Name"))
                    cellColor = ConsoleColor.Yellow;
                else if (val.EndsWith(" KB") || val.EndsWith(" MB") || val.EndsWith(" GB"))
                    cellColor = ConsoleColor.Yellow;
                else if (DateTime.TryParse(val, out _))
                    cellColor = ConsoleColor.Magenta;
                else if (val == "Directory")
                    cellColor = ConsoleColor.Blue;
                else if (val == "File")
                    cellColor = ConsoleColor.DarkCyan;

                Console.ForegroundColor = cellColor;
                Console.Write(val.PadRight(widths[i] + 2));
                Console.ResetColor();
            }
            Console.WriteLine();
        }

        static void RenderTableRow(List<string> cells, List<int> widths, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            var sb = new StringBuilder();
            for (int i = 0; i < cells.Count; i++)
                sb.Append(cells[i].PadRight(widths[i] + 2));
            Console.WriteLine(sb.ToString());
            Console.ResetColor();
        }

        static void RenderSeparator(List<int> widths)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(string.Join("  ", widths.Select(w => new string('─', w))));
            Console.ResetColor();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  MODULE 2 — INTERACTIVE REPL KEYSTROKE INTERCEPTOR ENGINE
    // ═══════════════════════════════════════════════════════════════════════════

    sealed class SessionState
    {
        public Dictionary<string, string> Variables { get; } = new();
        public List<string> History { get; } = new();

        public string ExpandVariables(string input)
        {
            foreach (var kv in Variables)
                input = input.Replace("$" + kv.Key, kv.Value);
            return input;
        }
    }

    static class InputReader
    {
        const string Prompt = "vortex > ";

        public static string ReadLine(List<string> history)
        {
            var buffer = new StringBuilder();
            int historyIndex = history.Count;
            int cursorPos = 0;

            while (true)
            {
                var key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return buffer.ToString();
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (cursorPos > 0)
                    {
                        buffer.Remove(cursorPos - 1, 1);
                        cursorPos--;
                        RedrawLine(buffer.ToString(), cursorPos);
                    }
                    continue;
                }

                if (key.Key == ConsoleKey.Delete)
                {
                    if (cursorPos < buffer.Length)
                    {
                        buffer.Remove(cursorPos, 1);
                        RedrawLine(buffer.ToString(), cursorPos);
                    }
                    continue;
                }

                if (key.Key == ConsoleKey.LeftArrow)
                {
                    if (cursorPos > 0)
                    {
                        cursorPos--;
                        Console.SetCursorPosition(Prompt.Length + cursorPos, Console.CursorTop);
                    }
                    continue;
                }

                if (key.Key == ConsoleKey.RightArrow)
                {
                    if (cursorPos < buffer.Length)
                    {
                        cursorPos++;
                        Console.SetCursorPosition(Prompt.Length + cursorPos, Console.CursorTop);
                    }
                    continue;
                }

                if (key.Key == ConsoleKey.UpArrow)
                {
                    if (history.Count == 0) continue;
                    historyIndex = Math.Max(0, historyIndex - 1);
                    buffer.Clear().Append(history[historyIndex]);
                    cursorPos = buffer.Length;
                    RedrawLine(buffer.ToString(), cursorPos);
                    continue;
                }

                if (key.Key == ConsoleKey.DownArrow)
                {
                    historyIndex = Math.Min(history.Count, historyIndex + 1);
                    string replacement = historyIndex < history.Count ? history[historyIndex] : "";
                    buffer.Clear().Append(replacement);
                    cursorPos = buffer.Length;
                    RedrawLine(buffer.ToString(), cursorPos);
                    continue;
                }

                if (key.Key == ConsoleKey.Home)
                {
                    cursorPos = 0;
                    Console.SetCursorPosition(Prompt.Length, Console.CursorTop);
                    continue;
                }

                if (key.Key == ConsoleKey.End)
                {
                    cursorPos = buffer.Length;
                    Console.SetCursorPosition(Prompt.Length + cursorPos, Console.CursorTop);
                    continue;
                }

                if (key.Key == ConsoleKey.Tab)
                {
                    string partial = buffer.ToString();
                    string? completed = TryComplete(partial);
                    if (completed != null)
                    {
                        buffer.Clear().Append(completed);
                        cursorPos = buffer.Length;
                        RedrawLine(buffer.ToString(), cursorPos);
                    }
                    continue;
                }

                if (key.KeyChar != '\0')
                {
                    buffer.Insert(cursorPos, key.KeyChar);
                    cursorPos++;
                    RedrawLine(buffer.ToString(), cursorPos);
                }
            }
        }

        static string? TryComplete(string partial)
        {
            var tokens = partial.TrimStart().Split(' ');
            if (tokens.Length < 2) return null;

            string prefix = tokens[^1];
            string dir = Directory.GetCurrentDirectory();

            try
            {
                var entries = Directory.GetFileSystemEntries(dir)
                    .Select(Path.GetFileName)
                    .Where(n => n != null && n!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(n => n)
                    .ToList();

                if (entries.Count == 1)
                {
                    tokens[^1] = entries[0]!.Contains(' ') ? $"\"{entries[0]}\"" : entries[0]!;
                    return string.Join(' ', tokens);
                }
            }
            catch { }

            return null;
        }

        static void RedrawLine(string content, int cursorPos)
        {
            int row = Console.CursorTop;
            int promptLen = Prompt.Length;
            Console.SetCursorPosition(promptLen, row);
            int clearWidth = Math.Max(0, Console.WindowWidth - promptLen - 1);
            Console.Write(new string(' ', clearWidth));
            Console.SetCursorPosition(promptLen, row);
            Console.Write(content);
            Console.SetCursorPosition(promptLen + cursorPos, row);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  MODULE 3 — WINDOWS TERMINAL FRAGMENT INTEGRATION & AUTO DEPLOYMENT
    // ═══════════════════════════════════════════════════════════════════════════

    static class TerminalDeployment
    {
        const string FragmentSubPath = @"Microsoft\Windows Terminal\Fragments\VortexShell";
        const string ProfileFileName = "vortex-profile.json";

        public static void AutoDeployTerminalStyles()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

            try
            {
                string? localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(localAppData)) return;

                string fragmentDir = Path.Combine(localAppData, FragmentSubPath);
                string profilePath = Path.Combine(fragmentDir, ProfileFileName);

                if (!Directory.Exists(fragmentDir))
                    Directory.CreateDirectory(fragmentDir);

                if (!File.Exists(profilePath))
                {
                    File.WriteAllText(profilePath, BuildProfileJson(), Encoding.UTF8);

                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine("  [vortex] Windows Terminal profile deployed to Fragments.");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"  [vortex] Terminal fragment deploy skipped: {ex.Message}");
                Console.ResetColor();
            }
        }

        static string BuildProfileJson()
        {
            string? exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "vortex.exe";

            return $$"""
{
  "$help": "https://aka.ms/terminal-documentation",
  "profiles": [
    {
      "name": "Vortex Shell",
      "guid": "{f8a2c3d4-b5e6-4f7a-8c9d-0e1f2a3b4c5d}",
      "commandline": "{{exePath.Replace("\\", "\\\\")}}",
      "icon": "\uE756",
      "font": {
        "face": "Cascadia Code",
        "size": 13,
        "ligatures": true,
        "weight": "normal"
      },
      "cursorShape": "filledBox",
      "cursorColor": "#00FFFF",
      "colorScheme": "VortexCyan",
      "useAcrylic": true,
      "acrylicOpacity": 0.65,
      "padding": "8, 8, 8, 8",
      "scrollbarState": "hidden",
      "startingDirectory": "%USERPROFILE%"
    }
  ],
  "schemes": [
    {
      "name": "VortexCyan",
      "background": "#0A0E14",
      "foreground": "#E6E1CF",
      "cursorColor": "#00FFFF",
      "selectionBackground": "#00FFFF33",
      "black": "#0A0E14",
      "blue": "#39BAE6",
      "brightBlack": "#626A73",
      "brightBlue": "#59C2FF",
      "brightCyan": "#00FFFF",
      "brightGreen": "#7FD962",
      "brightPurple": "#D2A6FF",
      "brightRed": "#FF6666",
      "brightWhite": "#E6E1CF",
      "brightYellow": "#FFD580",
      "cyan": "#95E6CB",
      "green": "#AAD94C",
      "purple": "#CFBAFA",
      "red": "#F07178",
      "white": "#C7C7C7",
      "yellow": "#E6B450"
    }
  ]
}
""";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  MODULE 4 — MULTI-PLATFORM RUNTIME ROUTER & UNMANAGED STREAM INTERCEPTION
    // ═══════════════════════════════════════════════════════════════════════════

    static class ExternalRunner
    {
        public static PipelineResult Run(string command)
        {
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            bool isMacOS   = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

            string shell;
            string flag;

            if (isWindows)
            {
                shell = "cmd.exe";
                flag  = "/c";
            }
            else
            {
                shell = "/bin/sh";
                flag  = "-c";
            }

            string arch = RuntimeInformation.ProcessArchitecture.ToString();

            var psi = new ProcessStartInfo
            {
                FileName               = shell,
                Arguments              = $"{flag} {command}",
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding  = Encoding.UTF8
            };

            if (!isWindows)
            {
                psi.Environment["TERM"] = "xterm-256color";
            }

            using var process = new Process { StartInfo = psi };

            var output = new List<string>();
            var errors = new List<string>();

            process.OutputDataReceived += (_, e) => { if (e.Data != null) output.Add(e.Data); };
            process.ErrorDataReceived  += (_, e) => { if (e.Data != null) errors.Add(e.Data); };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  vortex: command not found: {command.Split(' ')[0]}");
                Console.WriteLine($"  {ex.Message}");
                Console.ResetColor();
                return PipelineResult.Empty();
            }

            foreach (var err in errors)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(err);
                Console.ResetColor();
            }

            return PipelineResult.FromRaw(output);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  MODULE 5 — NATIVE CORE FILE ENGINE & SPECIAL OPERATIONAL BUILT-INS
    // ═══════════════════════════════════════════════════════════════════════════

    static class BuiltIns
    {
        public static PipelineResult Pwd()
        {
            return PipelineResult.FromRaw(new[] { Directory.GetCurrentDirectory() });
        }

        public static PipelineResult Cd(string[] args)
        {
            if (args.Length == 0)
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                Directory.SetCurrentDirectory(home);
                return PipelineResult.Empty();
            }

            string rawPath = string.Join(" ", args).Trim('"').Trim('\'');
            string resolved = Path.IsPathRooted(rawPath)
                ? rawPath
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), rawPath));

            if (!Directory.Exists(resolved))
            {
                WriteError($"cd: no such directory: {rawPath}");
                return PipelineResult.Empty();
            }

            Directory.SetCurrentDirectory(resolved);
            return PipelineResult.Empty();
        }

        public static PipelineResult Ls(string[] args)
        {
            string target = args.Length > 0
                ? string.Join(" ", args).Trim('"')
                : Directory.GetCurrentDirectory();

            if (!Directory.Exists(target))
            {
                WriteError($"ls: no such directory: {target}");
                return PipelineResult.Empty();
            }

            var entries = new List<VortexObject>();

            foreach (var dir in Directory.GetDirectories(target).OrderBy(d => d))
            {
                var info = new DirectoryInfo(dir);
                entries.Add(new VortexObject(new Dictionary<string, object>
                {
                    ["Type"]     = "Directory",
                    ["Name"]     = info.Name,
                    ["Modified"] = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                    ["Size"]     = "-",
                    ["Attrs"]    = info.Attributes.ToString().Split(',')[0].Trim()
                }));
            }

            foreach (var file in Directory.GetFiles(target).OrderBy(f => f))
            {
                var info = new FileInfo(file);
                entries.Add(new VortexObject(new Dictionary<string, object>
                {
                    ["Type"]     = "File",
                    ["Name"]     = info.Name,
                    ["Modified"] = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                    ["Size"]     = FormatBytes(info.Length),
                    ["Attrs"]    = info.Attributes.ToString().Split(',')[0].Trim()
                }));
            }

            return PipelineResult.FromObjects(entries);
        }

        public static void VortexMatrix()
        {
            Console.Clear();
            Console.CursorVisible = false;

            int width  = Console.WindowWidth;
            int height = Console.WindowHeight;
            var rng    = new Random();
            var cols   = new int[width];

            for (int i = 0; i < width; i++)
                cols[i] = rng.Next(height);

            string chars = "アイウエオカキクケコサシスセソタチツテトナニヌネノABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789@#$%&";

            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var k = Console.ReadKey(true);
                    if (k.Key == ConsoleKey.Escape || k.Key == ConsoleKey.Spacebar)
                        break;
                }

                for (int col = 0; col < width - 1; col++)
                {
                    int row = cols[col];
                    try
                    {
                        Console.SetCursorPosition(col, row % height);
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write(chars[rng.Next(chars.Length)]);

                        if (row > 1)
                        {
                            Console.SetCursorPosition(col, (row - 1) % height);
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.Write(chars[rng.Next(chars.Length)]);
                        }

                        if (row > 3)
                        {
                            Console.SetCursorPosition(col, (row - 3) % height);
                            Console.ForegroundColor = ConsoleColor.DarkGreen;
                            Console.Write(chars[rng.Next(chars.Length)]);
                        }

                        if (row > 7)
                        {
                            Console.SetCursorPosition(col, (row - 7) % height);
                            Console.ForegroundColor = ConsoleColor.Black;
                            Console.Write(' ');
                        }
                    }
                    catch { }

                    cols[col] = (row + 1) % (height * 2);
                }

                Thread.Sleep(35);
            }

            Console.ResetColor();
            Console.Clear();
            Console.CursorVisible = true;
            Shell.PrintBanner();
        }

        public static PipelineResult VortexSys()
        {
            int threads    = Environment.ProcessorCount;
            long gcGen0    = GC.CollectionCount(0);
            long gcGen1    = GC.CollectionCount(1);
            long gcGen2    = GC.CollectionCount(2);
            long heapBytes = GC.GetTotalMemory(false);
            string os      = RuntimeInformation.OSDescription;
            string arch    = RuntimeInformation.ProcessArchitecture.ToString();
            string runtime = RuntimeInformation.FrameworkDescription;
            string machine = Environment.MachineName;
            string user    = Environment.UserName;
            long uptime    = Environment.TickCount64 / 1000;

            Console.WriteLine();
            BoxLine("VORTEX SYSTEM DIAGNOSTICS", 58);
            SysRow("Machine",      machine);
            SysRow("User",         user);
            SysRow("OS",           os.Length > 54 ? os[..54] : os);
            SysRow("Architecture", arch);
            SysRow("Runtime",      runtime);
            SysRow("CPU Threads",  threads.ToString());
            SysRow("Uptime",       $"{uptime / 3600}h {(uptime % 3600) / 60}m {uptime % 60}s");
            SysRow("GC Heap",      FormatBytes(heapBytes));
            SysRow("GC Gen0",      gcGen0.ToString() + " collections");
            SysRow("GC Gen1",      gcGen1.ToString() + " collections");
            SysRow("GC Gen2",      gcGen2.ToString() + " collections");
            BoxSeparator(58);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("  │  Thread Load     ");
            Console.ResetColor();
            DrawBar(Math.Min(threads * 8, 100), 32);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  │");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("  │  Heap Usage      ");
            Console.ResetColor();
            long maxHeap = 512L * 1024 * 1024;
            int heapPct  = (int)Math.Min(heapBytes * 100 / maxHeap, 100);
            DrawBar(heapPct, 32);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  │");
            Console.ResetColor();

            BoxFooter(58);
            Console.WriteLine();

            return PipelineResult.Empty();
        }

        public static void VortexTop()
        {
            Console.CursorVisible = false;

            while (true)
            {
                var processes = Process.GetProcesses()
                    .OrderByDescending(p =>
                    {
                        try { return p.WorkingSet64; }
                        catch { return 0L; }
                    })
                    .Take(10)
                    .ToList();

                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  ╔══════════════════════════════════════════════════════════════════════╗");
                Console.WriteLine($"  ║  VORTEX TOP  ·  {DateTime.Now:HH:mm:ss}  ·  Press Q or ESC to exit             ║");
                Console.WriteLine($"  ╚══════════════════════════════════════════════════════════════════════╝");
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine(new string('─', 74));
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  {"PID",-8}{"Process Name",-30}{"Memory",-14}{"Threads",-10}Status");
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine(new string('─', 74));
                Console.ResetColor();

                foreach (var p in processes)
                {
                    try
                    {
                        long mb      = p.WorkingSet64 / (1024 * 1024);
                        string name  = p.ProcessName.Length > 28
                            ? p.ProcessName[..28] + ".."
                            : p.ProcessName;
                        string status  = p.Responding ? "Running" : "Not Responding";
                        string memStr  = $"{mb} MB";

                        ConsoleColor memColor = mb > 500 ? ConsoleColor.Red
                                             : mb > 200 ? ConsoleColor.Yellow
                                             : ConsoleColor.Gray;

                        Console.ForegroundColor = ConsoleColor.DarkCyan;
                        Console.Write($"  {p.Id,-8}");
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write($"{name,-30}");
                        Console.ForegroundColor = memColor;
                        Console.Write($"{memStr,-14}");
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.Write($"{p.Threads.Count,-10}");
                        Console.ForegroundColor = p.Responding ? ConsoleColor.Green : ConsoleColor.Red;
                        Console.WriteLine(status);
                        Console.ResetColor();
                    }
                    catch { }
                }

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine(new string('─', 74));
                Console.ResetColor();

                var deadline = DateTime.UtcNow.AddMilliseconds(1000);
                while (DateTime.UtcNow < deadline)
                {
                    if (Console.KeyAvailable)
                    {
                        var k = Console.ReadKey(true);
                        if (k.Key == ConsoleKey.Q || k.Key == ConsoleKey.Escape)
                        {
                            Console.Clear();
                            Console.CursorVisible = true;
                            return;
                        }
                    }
                    Thread.Sleep(50);
                }
            }
        }

        // ── Private Helpers ───────────────────────────────────────────────────

        static void DrawBar(int percent, int width)
        {
            percent = Math.Clamp(percent, 0, 100);
            int filled = (int)(percent / 100.0 * width);
            ConsoleColor barColor = percent > 75 ? ConsoleColor.Red
                                  : percent > 40 ? ConsoleColor.Yellow
                                  : ConsoleColor.Green;
            Console.ForegroundColor = barColor;
            Console.Write($"[{new string('█', filled)}{new string('░', width - filled)}] {percent,3}%");
            Console.ResetColor();
        }

        static void BoxLine(string title, int width)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  ┌{new string('─', width)}┐");
            Console.WriteLine($"  │  {title.PadRight(width - 2)}│");
            Console.WriteLine($"  ├{new string('─', width)}┤");
            Console.ResetColor();
        }

        static void BoxSeparator(int width)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  ├{new string('─', width)}┤");
            Console.ResetColor();
        }

        static void BoxFooter(int width)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  └{new string('─', width)}┘");
            Console.ResetColor();
        }

        static void SysRow(string label, string value)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"  │  {label,-16}");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"{value,-40}│");
            Console.ResetColor();
        }

        public static void WriteError(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  {msg}");
            Console.ResetColor();
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F2} GB";
            if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F2} MB";
            if (bytes >= 1_024)         return $"{bytes / 1_024.0:F2} KB";
            return $"{bytes} B";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  MODULE 6 — NATIVE POWERSHELL CMDLET ALIAS BINDING & COMMAND EVALUATOR
    // ═══════════════════════════════════════════════════════════════════════════

    static class Evaluator
    {
        public static PipelineResult Evaluate(string input, SessionState session)
        {
            input = input.Trim();
            if (string.IsNullOrEmpty(input)) return PipelineResult.Empty();

            if (input.StartsWith("$"))
            {
                var parts = input[1..].Split('=', 2);
                if (parts.Length == 2)
                {
                    string varName  = parts[0].Trim();
                    string varValue = parts[1].Trim().Trim('"');
                    session.Variables[varName] = varValue;
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine($"  ${varName} = \"{varValue}\"");
                    Console.ResetColor();
                    return PipelineResult.Empty();
                }

                string lookupKey = input[1..].Trim();
                if (session.Variables.TryGetValue(lookupKey, out string? lookedUp))
                {
                    return PipelineResult.FromRaw(new[] { lookedUp });
                }
            }

            input = session.ExpandVariables(input);

            var tokens = Tokenize(input);
            if (tokens.Count == 0) return PipelineResult.Empty();

            string cmd   = tokens[0].ToLowerInvariant();
            string[] args = tokens.Skip(1).ToArray();

            return cmd switch
            {
                // ── pwd / Get-Location ────────────────────────────────────────
                "pwd"
                or "get-location"
                or "gl"                              => BuiltIns.Pwd(),

                // ── cd / Set-Location ─────────────────────────────────────────
                "cd"
                or "set-location"
                or "sl"                              => BuiltIns.Cd(args),

                // ── ls / dir / Get-ChildItem ──────────────────────────────────
                "ls"
                or "dir"
                or "get-childitem"
                or "gci"                             => BuiltIns.Ls(args),

                // ── Get-Process / ps ─────────────────────────────────────────
                "get-process"
                or "gps"
                or "ps"                              => GetProcessSnapshot(args),

                // ── Vortex Special Commands ───────────────────────────────────
                "vortex-sys"                         => BuiltIns.VortexSys(),
                "vortex-matrix"                      => RunMatrix(),
                "vortex-top"                         => RunTop(),

                // ── Terminal Utilities ────────────────────────────────────────
                "clear"
                or "cls"
                or "clear-host"                      => ClearScreen(),

                "exit"
                or "quit"
                or "exit-pssession"                  => Exit(),

                "help"
                or "get-help"
                or "man"                             => Help(),

                "history"
                or "get-history"
                or "h"                               => ShowHistory(session),

                // ── Write / Echo ──────────────────────────────────────────────
                "echo"
                or "write-host"
                or "write-output"                    => WriteOutput(args),

                // ── File Content ──────────────────────────────────────────────
                "cat"
                or "get-content"
                or "gc"
                or "type"                            => GetContent(args),

                // ── File / Dir Creation ───────────────────────────────────────
                "mkdir"
                or "new-item"
                or "md"                              => NewItem(args),

                // ── Remove ────────────────────────────────────────────────────
                "rm"
                or "remove-item"
                or "del"
                or "ri"                              => RemoveItem(args),

                // ── Copy ──────────────────────────────────────────────────────
                "cp"
                or "copy-item"
                or "copy"
                or "ci"                              => CopyItem(args),

                // ── Move ──────────────────────────────────────────────────────
                "mv"
                or "move-item"
                or "move"
                or "mi"                              => MoveItem(args),

                // ── Measure ───────────────────────────────────────────────────
                "measure-object"
                or "measure"                         => MeasureObject(args, session),

                // ── Env Vars ──────────────────────────────────────────────────
                "get-variable"
                or "gv"                              => GetVariable(args, session),

                "set-variable"
                or "sv"                              => SetVariable(args, session),

                "get-childitem env:"
                or "env"
                or "get-env"                         => GetEnvironment(),

                // ── Version ───────────────────────────────────────────────────
                "--version"
                or "-v"
                or "version"                         => ShowVersion(),

                // ── Fallthrough → OS Shell ────────────────────────────────────
                _                                    => ExternalRunner.Run(input)
            };
        }

        // ── PowerShell Cmdlet Implementations ────────────────────────────────

        static PipelineResult GetProcessSnapshot(string[] args)
        {
            IEnumerable<Process> processes;

            if (args.Length > 0)
            {
                string target = string.Join(" ", args).Trim();
                processes = Process.GetProcesses()
                    .Where(p => p.ProcessName.Contains(target, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                processes = Process.GetProcesses()
                    .OrderByDescending(p =>
                    {
                        try { return p.WorkingSet64; }
                        catch { return 0L; }
                    })
                    .Take(20);
            }

            var objs = new List<VortexObject>();
            foreach (var p in processes)
            {
                try
                {
                    objs.Add(new VortexObject(new Dictionary<string, object>
                    {
                        ["PID"]     = p.Id,
                        ["Name"]    = p.ProcessName,
                        ["Memory"]  = BuiltIns.FormatBytes(p.WorkingSet64),
                        ["Threads"] = p.Threads.Count,
                        ["Status"]  = p.Responding ? "Running" : "Suspended"
                    }));
                }
                catch { }
            }

            return PipelineResult.FromObjects(objs);
        }

        static PipelineResult WriteOutput(string[] args)
        {
            string text = string.Join(" ", args).Trim('"');
            return PipelineResult.FromRaw(new[] { text });
        }

        static PipelineResult GetContent(string[] args)
        {
            if (args.Length == 0)
            {
                BuiltIns.WriteError("get-content: missing file path");
                return PipelineResult.Empty();
            }

            string path = string.Join(" ", args).Trim('"');
            if (!Path.IsPathRooted(path))
                path = Path.Combine(Directory.GetCurrentDirectory(), path);

            if (!File.Exists(path))
            {
                BuiltIns.WriteError($"get-content: file not found: {path}");
                return PipelineResult.Empty();
            }

            try
            {
                return PipelineResult.FromRaw(File.ReadAllLines(path, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                BuiltIns.WriteError($"get-content: {ex.Message}");
                return PipelineResult.Empty();
            }
        }

        static PipelineResult NewItem(string[] args)
        {
            if (args.Length == 0)
            {
                BuiltIns.WriteError("new-item: missing path argument");
                return PipelineResult.Empty();
            }

            bool isFile = args.Contains("-ItemType") &&
                          args.SkipWhile(a => a != "-ItemType").Skip(1).FirstOrDefault()
                              ?.Equals("File", StringComparison.OrdinalIgnoreCase) == true;

            string rawPath = args[0].Trim('"');
            string fullPath = Path.IsPathRooted(rawPath)
                ? rawPath
                : Path.Combine(Directory.GetCurrentDirectory(), rawPath);

            try
            {
                if (isFile)
                {
                    File.WriteAllText(fullPath, "", Encoding.UTF8);
                    return PipelineResult.FromRaw(new[] { $"  Created file: {fullPath}" });
                }
                else
                {
                    Directory.CreateDirectory(fullPath);
                    return PipelineResult.FromRaw(new[] { $"  Created directory: {fullPath}" });
                }
            }
            catch (Exception ex)
            {
                BuiltIns.WriteError($"new-item: {ex.Message}");
                return PipelineResult.Empty();
            }
        }

        static PipelineResult RemoveItem(string[] args)
        {
            if (args.Length == 0)
            {
                BuiltIns.WriteError("remove-item: missing path argument");
                return PipelineResult.Empty();
            }

            string rawPath = string.Join(" ", args.Where(a => !a.StartsWith("-"))).Trim('"');
            string fullPath = Path.IsPathRooted(rawPath)
                ? rawPath
                : Path.Combine(Directory.GetCurrentDirectory(), rawPath);

            bool recurse = args.Any(a => a.Equals("-Recurse", StringComparison.OrdinalIgnoreCase) ||
                                         a.Equals("-r", StringComparison.OrdinalIgnoreCase));

            try
            {
                if (Directory.Exists(fullPath))
                {
                    Directory.Delete(fullPath, recurse);
                    return PipelineResult.FromRaw(new[] { $"  Removed directory: {fullPath}" });
                }
                else if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    return PipelineResult.FromRaw(new[] { $"  Removed file: {fullPath}" });
                }
                else
                {
                    BuiltIns.WriteError($"remove-item: path not found: {fullPath}");
                    return PipelineResult.Empty();
                }
            }
            catch (Exception ex)
            {
                BuiltIns.WriteError($"remove-item: {ex.Message}");
                return PipelineResult.Empty();
            }
        }

        static PipelineResult CopyItem(string[] args)
        {
            if (args.Length < 2)
            {
                BuiltIns.WriteError("copy-item: usage: cp <source> <destination>");
                return PipelineResult.Empty();
            }

            string src  = args[0].Trim('"');
            string dest = args[1].Trim('"');

            if (!Path.IsPathRooted(src))  src  = Path.Combine(Directory.GetCurrentDirectory(), src);
            if (!Path.IsPathRooted(dest)) dest = Path.Combine(Directory.GetCurrentDirectory(), dest);

            try
            {
                File.Copy(src, dest, overwrite: true);
                return PipelineResult.FromRaw(new[] { $"  Copied: {src} → {dest}" });
            }
            catch (Exception ex)
            {
                BuiltIns.WriteError($"copy-item: {ex.Message}");
                return PipelineResult.Empty();
            }
        }

        static PipelineResult MoveItem(string[] args)
        {
            if (args.Length < 2)
            {
                BuiltIns.WriteError("move-item: usage: mv <source> <destination>");
                return PipelineResult.Empty();
            }

            string src  = args[0].Trim('"');
            string dest = args[1].Trim('"');

            if (!Path.IsPathRooted(src))  src  = Path.Combine(Directory.GetCurrentDirectory(), src);
            if (!Path.IsPathRooted(dest)) dest = Path.Combine(Directory.GetCurrentDirectory(), dest);

            try
            {
                File.Move(src, dest, overwrite: true);
                return PipelineResult.FromRaw(new[] { $"  Moved: {src} → {dest}" });
            }
            catch (Exception ex)
            {
                BuiltIns.WriteError($"move-item: {ex.Message}");
                return PipelineResult.Empty();
            }
        }

        static PipelineResult MeasureObject(string[] args, SessionState session)
        {
            if (args.Length == 0)
            {
                BuiltIns.WriteError("measure-object: specify a file or directory path");
                return PipelineResult.Empty();
            }

            string rawPath = string.Join(" ", args).Trim('"');
            string fullPath = Path.IsPathRooted(rawPath)
                ? rawPath
                : Path.Combine(Directory.GetCurrentDirectory(), rawPath);

            if (File.Exists(fullPath))
            {
                var fi   = new FileInfo(fullPath);
                var lines = File.ReadAllLines(fullPath, Encoding.UTF8);
                return PipelineResult.FromObjects(new[]
                {
                    new VortexObject(new Dictionary<string, object>
                    {
                        ["Property"] = "File",
                        ["Name"]     = fi.Name,
                        ["Lines"]    = lines.Length,
                        ["Words"]    = lines.Sum(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length),
                        ["Chars"]    = lines.Sum(l => l.Length),
                        ["Size"]     = BuiltIns.FormatBytes(fi.Length)
                    })
                });
            }
            else if (Directory.Exists(fullPath))
            {
                var files = Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories);
                long totalSize = files.Sum(f =>
                {
                    try { return new FileInfo(f).Length; } catch { return 0L; }
                });
                return PipelineResult.FromObjects(new[]
                {
                    new VortexObject(new Dictionary<string, object>
                    {
                        ["Property"]    = "Directory",
                        ["Name"]        = Path.GetFileName(fullPath),
                        ["Files"]       = files.Length,
                        ["Directories"] = Directory.GetDirectories(fullPath, "*", SearchOption.AllDirectories).Length,
                        ["TotalSize"]   = BuiltIns.FormatBytes(totalSize)
                    })
                });
            }
            else
            {
                BuiltIns.WriteError($"measure-object: path not found: {fullPath}");
                return PipelineResult.Empty();
            }
        }

        static PipelineResult GetVariable(string[] args, SessionState session)
        {
            if (args.Length == 0)
            {
                var objs = session.Variables.Select(kv => new VortexObject(new Dictionary<string, object>
                {
                    ["Name"]  = "$" + kv.Key,
                    ["Value"] = kv.Value
                })).ToList();
                return objs.Count > 0
                    ? PipelineResult.FromObjects(objs)
                    : PipelineResult.FromRaw(new[] { "  No session variables defined." });
            }

            string varName = args[0].TrimStart('$');
            if (session.Variables.TryGetValue(varName, out string? val))
                return PipelineResult.FromRaw(new[] { val });

            BuiltIns.WriteError($"get-variable: variable not found: ${varName}");
            return PipelineResult.Empty();
        }

        static PipelineResult SetVariable(string[] args, SessionState session)
        {
            if (args.Length < 2)
            {
                BuiltIns.WriteError("set-variable: usage: set-variable <name> <value>");
                return PipelineResult.Empty();
            }
            string varName  = args[0].TrimStart('$');
            string varValue = string.Join(" ", args.Skip(1)).Trim('"');
            session.Variables[varName] = varValue;
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"  ${varName} = \"{varValue}\"");
            Console.ResetColor();
            return PipelineResult.Empty();
        }

        static PipelineResult GetEnvironment()
        {
            var objs = new List<VortexObject>();
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                objs.Add(new VortexObject(new Dictionary<string, object>
                {
                    ["Name"]  = entry.Key?.ToString() ?? "",
                    ["Value"] = entry.Value?.ToString() ?? ""
                }));
            }
            return PipelineResult.FromObjects(objs.OrderBy(o => o.Get("Name")?.ToString()).ToList());
        }

        static PipelineResult ShowVersion()
        {
            return PipelineResult.FromRaw(new[]
            {
                "",
                "  Vortex Shell v2.0.0",
                $"  Runtime : {RuntimeInformation.FrameworkDescription}",
                $"  Platform: {RuntimeInformation.OSDescription}",
                $"  Arch    : {RuntimeInformation.ProcessArchitecture}",
                ""
            });
        }

        // ── Core REPL Utilities ───────────────────────────────────────────────

        static List<string> Tokenize(string input)
        {
            var tokens  = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;
            char quoteChar = '"';

            foreach (char c in input)
            {
                if ((c == '"' || c == '\'') && !inQuotes)
                {
                    inQuotes  = true;
                    quoteChar = c;
                }
                else if (c == quoteChar && inQuotes)
                {
                    inQuotes = false;
                }
                else if (c == ' ' && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
                tokens.Add(current.ToString());

            return tokens;
        }

        static PipelineResult ClearScreen()
        {
            Console.Clear();
            return PipelineResult.Empty();
        }

        static PipelineResult Exit()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n  Goodbye from Vortex Shell.\n");
            Console.ResetColor();
            Environment.Exit(0);
            return PipelineResult.Empty();
        }

        static PipelineResult Help()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║              VORTEX SHELL — Command Reference v2.0.0                    ║");
            Console.WriteLine("  ║         Unified classic alias + PowerShell Verb-Noun syntax              ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            HelpSection("NAVIGATION");
            HelpRow("pwd",           "Get-Location  gl",     "Print current working directory");
            HelpRow("cd <path>",     "Set-Location  sl",     "Change directory (quoted paths supported)");
            HelpRow("ls [path]",     "Get-ChildItem gci dir","List directory as structured object table");

            HelpSection("FILE OPERATIONS");
            HelpRow("cat <file>",    "Get-Content   gc type","Read file contents line by line");
            HelpRow("mkdir <path>",  "New-Item      md",     "Create a new directory");
            HelpRow("rm <path>",     "Remove-Item   del ri", "Remove file or directory (-r for recurse)");
            HelpRow("cp <src> <dst>","Copy-Item     copy ci","Copy a file to a destination");
            HelpRow("mv <src> <dst>","Move-Item     move mi","Move or rename a file");
            HelpRow("measure <path>","Measure-Object",       "File/dir statistics: lines, words, size");

            HelpSection("PROCESS MANAGEMENT");
            HelpRow("ps [name]",     "Get-Process   gps",    "Process snapshot sorted by memory (Top 20)");
            HelpRow("vortex-top",    "",                     "Live process monitor (refreshes every 1s)");

            HelpSection("VARIABLES");
            HelpRow("$name = value", "Set-Variable  sv",     "Assign a session-scoped variable");
            HelpRow("$name",         "Get-Variable  gv",     "Retrieve a variable value");
            HelpRow("echo $name",    "Write-Host Write-Output","Expand and print value to console");
            HelpRow("env",           "Get-ChildItem env:",   "List all OS environment variables");

            HelpSection("OUTPUT");
            HelpRow("echo <text>",   "Write-Host Write-Output","Write text to the console");
            HelpRow("history",       "Get-History   h",      "Show command history for this session");

            HelpSection("VORTEX SPECIALS");
            HelpRow("vortex-sys",    "",                     "System diagnostics: OS, CPU, GC, uptime");
            HelpRow("vortex-matrix", "",                     "Digital rain animation (Space/Esc to exit)");
            HelpRow("vortex-top",    "",                     "Live process monitor (Q/Esc to exit)");

            HelpSection("SHELL CONTROL");
            HelpRow("clear",         "cls Clear-Host",       "Clear the terminal screen");
            HelpRow("exit",          "quit",                 "Exit Vortex Shell");
            HelpRow("help",          "Get-Help man",         "Show this command reference");
            HelpRow("--version",     "version",              "Print version and runtime info");

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  ─────────────────────────────────────────────────────────────────────────");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("  Any unrecognized command is forwarded to the native OS shell.");
            Console.WriteLine("  On Windows: cmd.exe /c <command>  ·  On macOS/Linux: /bin/sh -c <command>");
            Console.ResetColor();
            Console.WriteLine();

            return PipelineResult.Empty();
        }

        static void HelpSection(string title)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  ─────────────────────────────────────────────────────────────────────────");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  {title}");
            Console.ResetColor();
        }

        static void HelpRow(string alias, string cmdlet, string desc)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"  {alias,-20}");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write($"{cmdlet,-26}");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(desc);
            Console.ResetColor();
        }

        static PipelineResult ShowHistory(SessionState session)
        {
            if (session.History.Count == 0)
                return PipelineResult.FromRaw(new[] { "  No history recorded yet." });

            var lines = session.History
                .Select((h, i) => $"  {i + 1,4}  {h}")
                .ToList();
            return PipelineResult.FromRaw(lines);
        }

        static PipelineResult RunMatrix()
        {
            BuiltIns.VortexMatrix();
            return PipelineResult.Empty();
        }

        static PipelineResult RunTop()
        {
            BuiltIns.VortexTop();
            return PipelineResult.Empty();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  SHELL ENTRY POINT
    // ═══════════════════════════════════════════════════════════════════════════

    static class Shell
    {
        public static void PrintBanner()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
  ██╗   ██╗ ██████╗ ██████╗ ████████╗███████╗██╗  ██╗
  ██║   ██║██╔═══██╗██╔══██╗╚══██╔══╝██╔════╝╚██╗██╔╝
  ██║   ██║██║   ██║██████╔╝   ██║   █████╗   ╚███╔╝
  ╚██╗ ██╔╝██║   ██║██╔══██╗   ██║   ██╔══╝   ██╔██╗
   ╚████╔╝ ╚██████╔╝██║  ██║   ██║   ███████╗██╔╝ ██╗
    ╚═══╝   ╚═════╝ ╚═╝  ╚═╝   ╚═╝   ╚══════╝╚═╝  ╚═╝");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"  OdixCodez  ·  v2.0.0  ·  {RuntimeInformation.FrameworkDescription}");
            Console.WriteLine($"  {RuntimeInformation.OSDescription}  ·  {RuntimeInformation.ProcessArchitecture}");
            Console.WriteLine("  Type 'help' for commands. PowerShell Verb-Noun syntax supported.\n");
            Console.ResetColor();
        }

        static void WritePrompt()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("vortex > ");
            Console.ResetColor();
        }

        public static void Run()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding  = Encoding.UTF8;
            Console.Title          = "Vortex Shell v2.0.0";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                TerminalDeployment.AutoDeployTerminalStyles();

            PrintBanner();

            var session = new SessionState();

            while (true)
            {
                WritePrompt();

                string input = InputReader.ReadLine(session.History).Trim();

                if (string.IsNullOrWhiteSpace(input)) continue;

                if (!string.IsNullOrEmpty(input) &&
                    (session.History.Count == 0 || session.History[^1] != input))
                    session.History.Add(input);

                try
                {
                    var result = Evaluator.Evaluate(input, session);
                    Renderer.RenderResult(result);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  error: {ex.Message}");
                    Console.ResetColor();
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  PROGRAM
    // ═══════════════════════════════════════════════════════════════════════════

    static class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                switch (args[0])
                {
                    case "--version":
                    case "-v":
                        Console.WriteLine($"Vortex Shell v2.0.0 ({RuntimeInformation.FrameworkDescription})");
                        Console.WriteLine($"Platform: {RuntimeInformation.OSDescription}");
                        Console.WriteLine($"Arch    : {RuntimeInformation.ProcessArchitecture}");
                        return;

                    case "--deploy-terminal":
                        TerminalDeployment.AutoDeployTerminalStyles();
                        return;

                    case "-c":
                        if (args.Length > 1)
                        {
                            string cmd = string.Join(" ", args.Skip(1));
                            var session = new SessionState();
                            var result  = Evaluator.Evaluate(cmd, session);
                            Renderer.RenderResult(result);
                        }
                        return;
                }
            }

            Shell.Run();
        }
    }
}