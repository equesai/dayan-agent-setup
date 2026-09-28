// DayanSetup.cs -- "Run Dayan Agent on my device" for Windows (owner, 2026-09-25;
// Windows and Linux only since 2026-09-26).
//
// A student first installs the app Dayan Agent works in -- OpenCode, Claude
// Code or Codex -- from its maker's own page (owner, 2026-09-26: "the student
// would be guided to install and then Dayan would proceed with what is left"),
// then downloads DayanSetup-<code>.exe from Dayan's guided chat flow and runs
// it. It opens full screen, reads the choices made in the chat through the
// one-time <code> in its own file name, finds the app (a missing one is named,
// with its maker's page, and nothing changes -- this setup installs no app),
// lists every change and where it lands, and does nothing until the student
// agrees (owner, 2026-09-26: "It has to ask user for permissions"). It asks
// for the student's Dayan key (typed as '*'), checks it with Dayan, then:
//   1. connects the app to Dayan's model and tools (its settings merged, never
//      replaced: opencode.json / .claude\settings.json / .codex\config.toml),
//   2. adds Dayan's Tutor and Maker, in that app's own words,
//   3. optionally installs the Arduino tools (arduino-cli + the AVR core),
//   4. adds "Dayan Agent" to Installed apps, so it can be removed like any app,
//   5. says hello through Dayan, and opens the app.
// The key is never written to the log, the screen or a command line.
//
// Removing it (owner, 2026-09-27 -- free code signing through SignPath
// Foundation asks for a way to uninstall): DayanSetup.exe --uninstall, which
// is what the Installed apps entry runs. What the setup added goes; the values
// it replaced in the app's settings come back (it keeps a record of them in
// installed.json), and the app itself, the work folder and the Arduino board
// files stay.
//
// Built by build.py with the C# compiler that ships with Windows
// (.NET Framework 4.x), so it needs nothing else on the student's PC. The
// version comes from device_setup/VERSION (build.py writes it in).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;

// SignPath's rule: the product name is the project's name, the same in every
// signed build.
[assembly: AssemblyTitle("Dayan Agent Setup")]
[assembly: AssemblyDescription("Connects OpenCode, Claude Code or Codex on this computer to Dayan")]
[assembly: AssemblyProduct("Dayan Agent Setup")]
[assembly: AssemblyCompany("Eques AI")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Eques AI. MIT License.")]

namespace Dayan
{
    class SetupFailure : Exception
    {
        public SetupFailure(string message) : base(message) { }
    }

    static class Native
    {
        [DllImport("kernel32.dll")] public static extern IntPtr GetStdHandle(int handle);
        [DllImport("kernel32.dll")] public static extern bool GetConsoleMode(IntPtr handle, out uint mode);
        [DllImport("kernel32.dll")] public static extern bool SetConsoleMode(IntPtr handle, uint mode);
        [DllImport("kernel32.dll")] public static extern IntPtr GetConsoleWindow();
        [DllImport("kernel32.dll")] public static extern uint GetConsoleProcessList(uint[] list, uint count);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessageTimeout(IntPtr window, uint message, UIntPtr wparam, string lparam,
                                                       uint flags, uint timeout, out UIntPtr result);
    }

    // ── Screen ───────────────────────────────────────────────────────────────
    static class Screen
    {
        public static bool Vt;
        public static bool Fancy;
        const string Esc = "\u001b[";
        public const string Gold = "38;2;227;179;65";
        public const string Soft = "38;2;170;170;170";
        public const string Green = "38;2;90;200;120";
        public const string Red = "38;2;235;100;90";
        public const string White = "97";

        public static string Logo =
            "            ▄▄▄▄▄▄\n" +
            "       ▄▄█▀▀▀▀▀▀▀▀▀▀█▄▄\n" +
            "     ▄█▀          ▄   ▀█▄\n" +
            "   ▄█▀    ▄▄███████     ▀█▄\n" +
            "  ▄█▀   ▄███████████▄     █▄\n" +
            " ▄█    ██████████████▄     █▄\n" +
            " ██   ▄███████████████▄    ██\n" +
            " ██   ███████▄▀████████▄   ██\n" +
            " ██   ▀███████▄▄    ▀█▀    ██\n" +
            " ▀█    ▀██████████▄▄       █▀\n" +
            "  ▀█    ▀███████████      █▀\n" +
            "   ▀█▄▄▄██████████████▄▄▄█▀\n" +
            "     ▀██████████████████▀\n" +
            "       ▀▀████████████▀▀\n" +
            "           ▀▀▀▀▀▀▀▀";

        public static void Init()
        {
            try { Console.OutputEncoding = new UTF8Encoding(false); } catch { }
            try
            {
                IntPtr handle = Native.GetStdHandle(-11);
                uint mode;
                if (Native.GetConsoleMode(handle, out mode))
                    Vt = Native.SetConsoleMode(handle, mode | 0x0004 | 0x0001);
            }
            catch { Vt = false; }
            Fancy = Vt && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WT_SESSION"));
            try { Console.Title = "Dayan Setup"; } catch { }
            try { Console.CursorVisible = false; } catch { }
        }

        public static string Tick { get { return Fancy ? "✓" : "√"; } }
        public static string Cross { get { return Fancy ? "✗" : "x"; } }
        public static string Dot { get { return "•"; } }

        public static int Width
        {
            get { try { return Math.Max(40, Console.WindowWidth); } catch { return 80; } }
        }

        public static int Column
        {
            get { return Math.Max(2, (Width - 72) / 2); }
        }

        public static string Paint(string text, string color)
        {
            if (!Vt || string.IsNullOrEmpty(color)) return text;
            return Esc + color + "m" + text + Esc + "0m";
        }

        public static void Clear()
        {
            try { Console.Clear(); } catch { }
            if (Vt) Console.Write(Esc + "3J" + Esc + "H");
        }

        public static void Line(string text, string color)
        {
            Console.WriteLine(new string(' ', Column) + Paint(text, color));
        }

        public static void Line(string text) { Line(text, null); }

        public static void Blank() { Console.WriteLine(); }

        public static void Center(string text, string color)
        {
            int pad = Math.Max(0, (Width - text.Length) / 2);
            Console.WriteLine(new string(' ', pad) + Paint(text, color));
        }

        public static void Header(string subtitle)
        {
            Clear();
            Blank();
            string[] rows = Logo.Split('\n');
            int logoWidth = 0;
            foreach (string row in rows) logoWidth = Math.Max(logoWidth, row.Length);
            int pad = Math.Max(0, (Width - logoWidth) / 2);
            foreach (string row in rows) Console.WriteLine(new string(' ', pad) + Paint(row, Gold));
            Blank();
            Center("D A Y A N   A G E N T", Gold);
            Center(subtitle, Soft);
            Blank();
        }

        // Words wrapped to the column, each line indented.
        public static void Para(string text, string color)
        {
            int width = Math.Min(72, Width - Column - 2);
            foreach (string paragraph in text.Split('\n'))
            {
                string line = "";
                foreach (string word in paragraph.Split(' '))
                {
                    if (line.Length > 0 && line.Length + 1 + word.Length > width)
                    {
                        Line(line, color);
                        line = word;
                    }
                    else line = line.Length == 0 ? word : line + " " + word;
                }
                Line(line, color);
            }
        }

        public static void Para(string text) { Para(text, null); }

        public static ConsoleKey WaitFor(params ConsoleKey[] keys)
        {
            while (true)
            {
                ConsoleKeyInfo info = Console.ReadKey(true);
                foreach (ConsoleKey key in keys) if (info.Key == key) return key;
            }
        }
    }

    // ── One visible step with a live status ─────────────────────────────────
    class Step
    {
        public string Title;
        public string State = "todo";  // todo | run | done | fail | skip
        public string Note = "";
        public Step(string title) { Title = title; }
    }

    static class Program
    {
        static string Api = "https://dayan.equesai.tech";
        static string Home;
        static string ConfigDir;
        static string WorkDir;
        static string DataDir;
        static string LogPath;
        static bool NoInstall;
        static bool NoRegistry;
        static bool Yes;
        static bool NoOpen;
        static bool NoFullscreen;
        static bool Uninstalling;         // --uninstall: remove what the setup added
        static string StatePath;          // installed.json: what the setup changed, for removing it
        static Dictionary<string, object> State;
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\DayanAgent";
        static string KeyEnv;
        static string CodeArg;
        static string AppArg;
        static string Key;
        static string FirstName = "";
        static bool WantArduino;
        static string App = "opencode";   // opencode | claude | codex
        static string AppName = "OpenCode";
        static string AppExe;             // the app this setup found
        static bool AppIsDesktop;         // OpenCode's desktop app (else a Terminal app)
        static bool PathLine;             // the app's folder goes on the user PATH
        static List<Step> Steps = new List<Step>();
        static int Spin;
        static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        static readonly Dictionary<string, string> Names = new Dictionary<string, string> {
            { "opencode", "OpenCode" }, { "claude", "Claude Code" }, { "codex", "Codex" } };
        // Where each maker explains its own installation (the chat's buttons open the same pages).
        static readonly Dictionary<string, string> Pages = new Dictionary<string, string> {
            { "opencode", "https://opencode.ai/download" },
            { "claude", "https://code.claude.com/docs/en/setup#install-claude-code" },
            { "codex", "https://learn.chatgpt.com/docs/codex/cli" } };

        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                Parse(args);
                Screen.Init();
                Log((Uninstalling ? "removal " : "setup ") + BuildInfo.Version + " started ("
                    + BuildInfo.SourceSha256.Substring(0, 12) + ")");
                if (!NoFullscreen) FullScreen();
                return Uninstalling ? Uninstall() : Run();
            }
            catch (Exception error)
            {
                Log("fatal: " + error);
                try
                {
                    Screen.Blank();
                    Screen.Line(Screen.Cross + "  Something went wrong: " + error.Message, Screen.Red);
                    Screen.Line("   Details are in " + LogPath, Screen.Soft);
                    Screen.Blank();
                    Screen.Line("Press any key to close.", Screen.Soft);
                    if (!Yes) Console.ReadKey(true);
                }
                catch { }
                return 1;
            }
        }

        // The user's folders from the environment first (as every app reads
        // them), else from Windows.
        static string Folder(string variable, Environment.SpecialFolder fallback)
        {
            string value = Environment.GetEnvironmentVariable(variable);
            return string.IsNullOrEmpty(value) ? Environment.GetFolderPath(fallback) : value;
        }

        static void Parse(string[] args)
        {
            Home = Folder("USERPROFILE", Environment.SpecialFolder.UserProfile);
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                string next = i + 1 < args.Length ? args[i + 1] : null;
                if (arg == "--api" && next != null) { Api = next.TrimEnd('/'); i++; }
                else if (arg == "--home" && next != null) { Home = next; i++; }
                else if (arg == "--code" && next != null) { CodeArg = next; i++; }
                else if (arg == "--app" && next != null) { AppArg = next; i++; }
                else if (arg == "--key-env" && next != null) { KeyEnv = next; i++; }
                else if (arg == "--no-install") NoInstall = true;
                else if (arg == "--no-registry") NoRegistry = true;
                else if (arg == "--yes") Yes = true;
                else if (arg == "--no-open") NoOpen = true;
                else if (arg == "--no-fullscreen") NoFullscreen = true;
                else if (arg == "--uninstall") Uninstalling = true;
            }
            WorkDir = Path.Combine(Home, "Dayan");
            DataDir = Path.Combine(Folder("LOCALAPPDATA", Environment.SpecialFolder.LocalApplicationData), "Dayan");
            StatePath = Path.Combine(DataDir, "installed.json");
            if (Uninstalling)
            {
                // Dayan's own folder goes at the end of a removal: its record is kept aside.
                LogPath = Path.Combine(Path.GetTempPath(), "DayanSetup-uninstall.log");
                return;
            }
            Directory.CreateDirectory(DataDir);
            LogPath = Path.Combine(DataDir, "setup.log");
        }

        static void Log(string line)
        {
            try
            {
                File.AppendAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                                            + "  " + line + Environment.NewLine, new UTF8Encoding(false));
            }
            catch { }
        }

        // Full screen only in a window this program opened itself (a double
        // click), never in a terminal the student was already using.
        static void FullScreen()
        {
            try
            {
                uint[] list = new uint[8];
                if (Native.GetConsoleProcessList(list, 8) > 1) return;
                IntPtr window = Native.GetConsoleWindow();
                if (window != IntPtr.Zero)
                {
                    Native.SetForegroundWindow(window);
                    Native.ShowWindow(window, 3);  // maximise first: a fallback if Alt+Enter is ignored
                }
                Native.keybd_event(0x12, 0, 0, UIntPtr.Zero);  // Alt down
                Native.keybd_event(0x0D, 0, 0, UIntPtr.Zero);  // Enter
                Native.keybd_event(0x0D, 0, 2, UIntPtr.Zero);
                Native.keybd_event(0x12, 0, 2, UIntPtr.Zero);  // Alt up
                Thread.Sleep(350);
            }
            catch { }
        }

        // ── The run ────────────────────────────────────────────────────────
        static int Run()
        {
            State = LoadState();
            Dictionary<string, object> choices = LoadChoices();
            WantArduino = choices != null && Bool(choices, "arduino");
            FirstName = choices != null ? Str(choices, "first_name") : "";
            string chosen = AppArg ?? (choices != null ? Str(choices, "app") : "");
            App = Names.ContainsKey(chosen) ? chosen : "opencode";
            AppName = Names[App];
            ConfigDir = AppConfigDir();
            Log("app: " + App);

            // The app first: the student installed it from its maker's page.
            AppExe = FindApp();
            if (AppExe == null) return MissingApp();
            Log("found " + AppName + ": " + AppExe);
            string bin = Path.GetDirectoryName(AppExe);
            PathLine = !AppIsDesktop && bin.Equals(Path.Combine(Home, ".local", "bin"), StringComparison.OrdinalIgnoreCase)
                       && !OnUserPath(bin);

            if (!Welcome()) return Cancelled();
            if (!AskKey()) return Cancelled();

            if (App == "opencode") Steps.Add(new Step("Saving your key on this computer"));
            Steps.Add(new Step("Connecting " + AppName + " to Dayan"));
            Steps.Add(new Step("Adding Dayan's Tutor and Maker skills"));
            if (!AppIsDesktop) Steps.Add(new Step("Making your work folder"));
            if (WantArduino) Steps.Add(new Step("Installing the Arduino tools"));
            // A test run (--no-registry) leaves the registry alone.
            if (!NoRegistry) Steps.Add(new Step("Adding Dayan Agent to Installed apps"));
            Steps.Add(new Step("Saying hello to Dayan"));

            int index = 0;
            if (App == "opencode") Do(Steps[index++], SaveKey);
            Do(Steps[index++], ConnectApp);
            Do(Steps[index++], AddSkills);
            if (!AppIsDesktop) Do(Steps[index++], MakeWorkFolder);
            if (WantArduino) Do(Steps[index++], InstallArduino);
            if (!NoRegistry) Do(Steps[index++], Register);
            string hello = null;
            Do(Steps[index++], delegate (Step step) { hello = SayHello(step); });
            return Finish(hello);
        }

        static int Cancelled()
        {
            Log("cancelled by the user");
            Screen.Clear();
            Screen.Blank();
            Screen.Line("Nothing was changed. You can run Dayan Setup again any time.", Screen.Soft);
            Screen.Blank();
            Thread.Sleep(Yes ? 0 : 1800);
            return 2;
        }

        static Dictionary<string, object> LoadChoices()
        {
            string code = CodeArg;
            if (code == null)
            {
                string name = Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
                Match match = Regex.Match(name, @"DayanSetup[-_ ]?([a-z0-9]{8,32})", RegexOptions.IgnoreCase);
                if (match.Success) code = match.Groups[1].Value.ToLowerInvariant();
            }
            if (code == null) { Log("no setup code in the file name; asking here instead"); return AskChoices(); }
            try
            {
                Dictionary<string, object> data = GetJson(Api + "/api/device-setup/" + code, null);
                Log("choices loaded for code " + code.Substring(0, 4) + "…");
                return data;
            }
            catch (Exception error)
            {
                Log("choices for the code could not be read: " + error.Message);
                return AskChoices();
            }
        }

        // Only when the file was renamed or the code expired.
        static Dictionary<string, object> AskChoices()
        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["first_name"] = "";
            data["app"] = AppArg ?? "opencode";
            if (AppArg == null && !Yes)
            {
                Screen.Header("Set up Dayan Agent on this computer");
                Screen.Para("Which app will Dayan Agent work in?");
                Screen.Blank();
                Screen.Line("1  OpenCode", Screen.White);
                Screen.Line("2  Claude Code", Screen.White);
                Screen.Line("3  Codex", Screen.White);
                ConsoleKey pick = Screen.WaitFor(ConsoleKey.D1, ConsoleKey.D2, ConsoleKey.D3,
                                                 ConsoleKey.NumPad1, ConsoleKey.NumPad2, ConsoleKey.NumPad3);
                data["app"] = pick == ConsoleKey.D2 || pick == ConsoleKey.NumPad2 ? "claude"
                            : pick == ConsoleKey.D3 || pick == ConsoleKey.NumPad3 ? "codex" : "opencode";
            }
            Screen.Header("Set up Dayan Agent on this computer");
            Screen.Para("One question before we start: will you build Arduino projects with Dayan?");
            Screen.Blank();
            Screen.Line("Y  Yes, add the Arduino tools", Screen.White);
            Screen.Line("N  Not now", Screen.White);
            ConsoleKey key = Yes ? ConsoleKey.N : Screen.WaitFor(ConsoleKey.Y, ConsoleKey.N);
            data["arduino"] = key == ConsoleKey.Y;
            return data;
        }

        // ── Finding the app (installed by the student from its maker's page) ─
        static string AppConfigDir() { return AppConfigDir(App); }

        static string AppConfigDir(string app)
        {
            string explicitDir;
            if (app == "claude")
            {
                explicitDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
                return string.IsNullOrEmpty(explicitDir) ? Path.Combine(Home, ".claude") : explicitDir;
            }
            if (app == "codex")
            {
                explicitDir = Environment.GetEnvironmentVariable("CODEX_HOME");
                return string.IsNullOrEmpty(explicitDir) ? Path.Combine(Home, ".codex") : explicitDir;
            }
            explicitDir = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            return Path.Combine(string.IsNullOrEmpty(explicitDir) ? Path.Combine(Home, ".config") : explicitDir, "opencode");
        }

        // The folders a new window's PATH holds: this process's, and the ones
        // Windows keeps for the user and the machine (an installer that just
        // ran changed those only).
        static List<string> PathFolders()
        {
            List<string> folders = new List<string>();
            // A test run (--no-registry) neither changes nor reads the registry's PATH.
            string[] parts = NoRegistry ? new[] { Environment.GetEnvironmentVariable("PATH") } : new[] {
                Environment.GetEnvironmentVariable("PATH"),
                Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User),
                Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) };
            foreach (string part in parts)
                if (!string.IsNullOrEmpty(part))
                    foreach (string folder in part.Split(';'))
                        if (folder.Trim().Length > 0) folders.Add(Environment.ExpandEnvironmentVariables(folder.Trim()));
            return folders;
        }

        static string FindProgram(string[] files, params string[] places)
        {
            foreach (string place in places)
                if (!string.IsNullOrEmpty(place) && File.Exists(place)) return place;
            foreach (string folder in PathFolders())
                foreach (string file in files)
                {
                    try
                    {
                        string candidate = Path.Combine(folder, file);
                        if (File.Exists(candidate)) return candidate;
                    }
                    catch (ArgumentException) { }  // a PATH entry with characters a path cannot hold
                }
            return null;
        }

        static string FindApp()
        {
            string local = Folder("LOCALAPPDATA", Environment.SpecialFolder.LocalApplicationData);
            string roaming = Folder("APPDATA", Environment.SpecialFolder.ApplicationData);
            AppIsDesktop = false;
            if (App == "claude")
                return FindProgram(new[] { "claude.exe", "claude.cmd" },
                                   Path.Combine(Home, @".local\bin\claude.exe"), Path.Combine(roaming, @"npm\claude.cmd"),
                                   Path.Combine(local, @"Microsoft\WinGet\Links\claude.exe"));
            if (App == "codex")
                return FindProgram(new[] { "codex.exe", "codex.cmd" },
                                   Path.Combine(local, @"Programs\OpenAI\Codex\bin\codex.exe"),
                                   Path.Combine(roaming, @"npm\codex.cmd"), Path.Combine(local, @"Microsoft\WinGet\Links\codex.exe"));
            string desktop = FindOpenCode();
            if (desktop != null) { AppIsDesktop = true; return desktop; }
            return FindProgram(new[] { "opencode.exe", "opencode.cmd" }, Path.Combine(Home, @".opencode\bin\opencode.exe"));
        }

        static string FindOpenCode()
        {
            List<string> candidates = new List<string>();
            try
            {
                // A test run (--no-registry) looks only in the folders it was given.
                using (RegistryKey root = NoRegistry ? null
                       : Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (root != null)
                        foreach (string name in root.GetSubKeyNames())
                            using (RegistryKey entry = root.OpenSubKey(name))
                            {
                                if (entry == null) continue;
                                string display = entry.GetValue("DisplayName") as string;
                                if (display == null || !display.StartsWith("OpenCode", StringComparison.OrdinalIgnoreCase)) continue;
                                string location = entry.GetValue("InstallLocation") as string;
                                if (!string.IsNullOrEmpty(location)) candidates.Add(Path.Combine(location, "OpenCode.exe"));
                                string icon = entry.GetValue("DisplayIcon") as string;
                                if (!string.IsNullOrEmpty(icon)) candidates.Add(icon.Split(',')[0].Trim('"'));
                            }
                }
            }
            catch { }
            string local = Folder("LOCALAPPDATA", Environment.SpecialFolder.LocalApplicationData);
            candidates.Add(Path.Combine(local, @"Programs\@opencode-aidesktop\OpenCode.exe"));
            candidates.Add(Path.Combine(local, @"Programs\@opencodedesktop\OpenCode.exe"));
            candidates.Add(Path.Combine(local, @"Programs\OpenCode\OpenCode.exe"));
            foreach (string candidate in candidates)
                if (candidate.EndsWith("OpenCode.exe", StringComparison.OrdinalIgnoreCase) && File.Exists(candidate))
                    return candidate;
            return null;
        }

        static int MissingApp()
        {
            Log(AppName + " not found: nothing changed");
            Screen.Header("Install " + AppName + " first");
            Screen.Para("Dayan Agent works inside " + AppName + ", and it isn't on this computer yet. Install it from "
                        + "its maker's own page -- the button in Dayan's chat opens it and says which part of the page "
                        + "to use -- then open this file again. Nothing was changed.", Screen.White);
            Screen.Blank();
            Screen.Line(Pages[App], Screen.Gold);
            Screen.Blank();
            Screen.Line("Press Enter to open that page   " + Screen.Dot + "   Esc to close", Screen.Gold);
            if (!Yes && Screen.WaitFor(ConsoleKey.Enter, ConsoleKey.Escape) == ConsoleKey.Enter)
            {
                try { Process.Start(new ProcessStartInfo(Pages[App]) { UseShellExecute = true }); }
                catch (Exception error) { Log("could not open the page: " + error.Message); }
            }
            return 3;
        }

        static bool Welcome()
        {
            Screen.Header("Set up Dayan Agent on this computer");
            string hi = string.IsNullOrEmpty(FirstName) ? "Hi!" : "Hi " + FirstName + "!";
            Screen.Para(hi + " " + AppName + " is on this computer. In a few minutes it will be ready for you to work "
                        + "with Dayan Agent: your tutor for what you study, and your partner for building websites, "
                        + "games and Arduino projects.", Screen.White);
            Screen.Blank();
            // Every change, and where it lands: the student agrees to this list
            // before anything happens (owner, 2026-09-26).
            Screen.Line("Here's what I'll set up -- nothing else on this computer changes:", Screen.Gold);
            Screen.Blank();
            if (App == "claude")
            {
                Item("Your Dayan key", "in Claude Code's settings (.claude), for you only");
                Item("Claude Code settings", "Dayan's AI and tools; your own settings are kept");
                Item("Dayan's skills", "Tutor and Maker, with Maker's helpers");
            }
            else if (App == "codex")
            {
                Item("Your Dayan key", "in Codex's settings (.codex), for you only");
                Item("Codex settings", "Dayan's AI and tools; your own lines are kept");
                Item("Dayan's skills", "Tutor and Maker (.agents\\skills)");
            }
            else
            {
                Item("Your Dayan key", "kept in your user folder (.config\\opencode)");
                Item("OpenCode settings", "connected to Dayan; your own settings are kept");
                Item("Dayan's skills", "Tutor for learning, Maker for projects");
            }
            if (!AppIsDesktop) Item("A work folder", "Dayan, in your user folder: " + AppName + " starts there");
            if (PathLine) Item("Your PATH", "so a new Terminal finds " + Path.GetFileNameWithoutExtension(AppExe));
            if (WantArduino) Item("Arduino tools", "arduino-cli + board files (~400 MB), on your PATH");
            if (!NoRegistry) Item("Installed apps", "\"Dayan Agent\", to remove all of this any time");
            Screen.Blank();
            Screen.Para("No administrator rights: nothing outside your own account changes. A record of every "
                        + "step is kept in " + LogPath + " (never your key).", Screen.Soft);
            // What it sends, and to whom (SignPath's rule: said before the setup starts).
            Screen.Para("It talks only to Dayan (" + Host(Api) + ")"
                        + (WantArduino ? " and, for the Arduino tools, Arduino's downloads (GitHub, arduino.cc)" : "")
                        + ": to read your choices, check your key and say hello. It sends nothing else about this computer.",
                        Screen.Soft);
            Screen.Blank();
            Screen.Line("Press Enter to agree and start   " + Screen.Dot + "   Esc to cancel", Screen.Gold);
            if (Yes) return true;
            return Screen.WaitFor(ConsoleKey.Enter, ConsoleKey.Escape) == ConsoleKey.Enter;
        }

        static void Item(string name, string what)
        {
            Screen.Line("  " + Screen.Paint("◆", Screen.Gold) + "  " + Screen.Paint(name.PadRight(21), Screen.White)
                        + Screen.Paint(what, Screen.Soft));
        }

        static bool AskKey()
        {
            string problem = null;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Screen.Header("Your Dayan key");
                Screen.Para("Your key connects this computer to your Dayan account. To get it:", Screen.White);
                Screen.Blank();
                Screen.Line("1.  Open Dayan, then Settings  ›  Device keys", Screen.White);
                Screen.Line("2.  Click \"Generate a new key\", then Copy", Screen.White);
                Screen.Line("3.  Paste it below (Ctrl+V or right-click) and press Enter", Screen.White);
                Screen.Blank();
                if (problem != null) { Screen.Para(Screen.Cross + "  " + problem, Screen.Red); Screen.Blank(); }
                string key;
                if (KeyEnv != null) key = (Environment.GetEnvironmentVariable(KeyEnv) ?? "").Trim();
                else
                {
                    Console.Write(new string(' ', Screen.Column) + Screen.Paint("Key: ", Screen.Gold));
                    try { Console.CursorVisible = true; } catch { }
                    key = ReadMasked();
                    try { Console.CursorVisible = false; } catch { }
                    Console.WriteLine();
                    if (key == null) return false;
                }
                if (!Regex.IsMatch(key, @"^dyn_[A-Za-z0-9_-]{40,64}$"))
                {
                    problem = key.Length == 0 ? "Paste your key first." :
                              "That doesn't look like a Dayan key. It starts with dyn_ -- copy it again from Settings.";
                    if (KeyEnv != null) throw new SetupFailure(problem);
                    continue;
                }
                Screen.Line("Checking your key…", Screen.Soft);
                try
                {
                    Dictionary<string, object> me = GetJson(Api + "/api/llm/whoami", key);
                    Key = key;
                    string first = Str(me, "first_name");
                    if (first.Length > 0) FirstName = first;
                    Log("key accepted (role " + Str(me, "role") + ")");
                    return true;
                }
                catch (WebException error)
                {
                    int status = Status(error);
                    problem = status == 401 ? "Dayan doesn't recognise this key -- it may have been revoked. Generate a new one and paste it."
                            : status == 403 ? "Keys are switched off for your account. Ask your Dayan administrator."
                            : status == 404 ? "Keys aren't available on this Dayan right now."
                            : "Dayan can't be reached right now. Check your internet connection and try again.";
                    Log("key check failed: HTTP " + status + " " + error.Message);
                    if (KeyEnv != null) throw new SetupFailure(problem);
                }
            }
            return false;
        }

        static string ReadMasked()
        {
            StringBuilder text = new StringBuilder();
            while (true)
            {
                ConsoleKeyInfo info = Console.ReadKey(true);
                if (info.Key == ConsoleKey.Enter) break;
                if (info.Key == ConsoleKey.Escape) return null;
                if (info.Key == ConsoleKey.Backspace)
                {
                    if (text.Length > 0) { text.Length -= 1; Console.Write("\b \b"); }
                    continue;
                }
                char c = info.KeyChar;
                if (c > ' ' && c < 127) { text.Append(c); Console.Write('*'); }
            }
            return text.ToString().Trim();
        }

        // ── Steps ──────────────────────────────────────────────────────────
        delegate void Work(Step step);

        static void Do(Step step, Work work)
        {
            while (true)
            {
                step.State = "run";
                step.Note = "";
                Draw();
                try
                {
                    work(step);
                    if (step.State == "run") step.State = "done";
                    Log("done: " + step.Title + (step.Note.Length > 0 ? " (" + step.Note + ")" : ""));
                    Draw();
                    return;
                }
                catch (Exception error)
                {
                    step.State = "fail";
                    step.Note = error is SetupFailure ? error.Message : "Something went wrong: " + error.Message;
                    Log("failed: " + step.Title + ": " + error);
                    Draw();
                    Screen.Blank();
                    Screen.Line("Press R to try again   " + Screen.Dot + "   Esc to stop", Screen.Gold);
                    if (Yes) throw;
                    if (Screen.WaitFor(ConsoleKey.R, ConsoleKey.Escape) == ConsoleKey.Escape)
                        throw new SetupFailure("Setup stopped at: " + step.Title);
                }
            }
        }

        static void Draw()
        {
            Screen.Header(Uninstalling ? "Removing Dayan Agent" : "Setting things up");
            if (!Uninstalling)
                Screen.Line(Screen.Tick + "  Key accepted" + (FirstName.Length > 0 ? " -- hello, " + FirstName : ""), Screen.Green);
            foreach (Step step in Steps)
            {
                string mark, color;
                switch (step.State)
                {
                    case "done": mark = Screen.Tick; color = Screen.Green; break;
                    case "fail": mark = Screen.Cross; color = Screen.Red; break;
                    case "skip": mark = "–"; color = Screen.Soft; break;
                    case "run": mark = Spinner(); color = Screen.Gold; break;
                    default: mark = "·"; color = Screen.Soft; break;
                }
                string text = mark + "  " + step.Title + (step.Note.Length > 0 ? "  " + step.Note : "");
                Screen.Line(text, color);
            }
        }

        static string Spinner()
        {
            string frames = Screen.Fancy ? "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏" : "|/-\\";
            Spin = (Spin + 1) % frames.Length;
            return frames[Spin].ToString();
        }

        static void Progress(Step step, string note)
        {
            step.Note = note;
            Draw();
        }

        // The key file is written before the config that points at it: a
        // config naming a missing {file:} does not load at all.
        static void SaveKey(Step step)
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(Path.Combine(ConfigDir, "dayan-key"), Key, new UTF8Encoding(false));
            step.Note = "kept in your user folder";
        }

        static void ConnectApp(Step step)
        {
            Directory.CreateDirectory(ConfigDir);
            if (App == "claude") ConnectClaude(step);
            else if (App == "codex") ConnectCodex(step);
            else ConnectOpenCode(step);
            if (step.Note.Length == 0) step.Note = "done";
        }

        // A JSON settings file, merged: an unreadable one is kept as a copy.
        static Dictionary<string, object> ReadSettings(string path, Step step)
        {
            Dictionary<string, object> config = new Dictionary<string, object>();
            if (!File.Exists(path)) return config;
            string text = File.ReadAllText(path).TrimStart('﻿');
            object parsed = null;
            try { parsed = Json.DeserializeObject(text); } catch { }
            if (parsed is Dictionary<string, object>) return (Dictionary<string, object>)parsed;
            string backup = path + ".before-dayan-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            File.Copy(path, backup, true);
            Log(Path.GetFileName(path) + " could not be read; kept a copy at " + backup);
            step.Note = "your old settings were kept as a copy";
            return config;
        }

        static readonly string[] OpenCodeKeys = { "model", "small_model", "default_agent" };
        static readonly string[] ClaudeEnv = {
            "ANTHROPIC_BASE_URL", "ANTHROPIC_AUTH_TOKEN", "DAYAN_API_KEY", "ANTHROPIC_MODEL",
            "ANTHROPIC_DEFAULT_OPUS_MODEL", "ANTHROPIC_DEFAULT_SONNET_MODEL", "ANTHROPIC_DEFAULT_HAIKU_MODEL",
            "CLAUDE_CODE_SUBAGENT_MODEL", "DISABLE_TELEMETRY", "DISABLE_ERROR_REPORTING" };

        static void ConnectOpenCode(Step step)
        {
            string path = Path.Combine(ConfigDir, "opencode.json");
            Dictionary<string, object> mine = AppState("opencode");
            if (!mine.ContainsKey("created_config")) mine["created_config"] = !File.Exists(path);
            Dictionary<string, object> config = ReadSettings(path, step);
            RememberBefore(mine, config, OpenCodeKeys);
            if (!config.ContainsKey("$schema")) config["$schema"] = "https://opencode.ai/config.json";

            Dictionary<string, object> models = new Dictionary<string, object>();
            models["dayan"] = Model("Dayan");
            models["dayan-pro"] = Model("Dayan Pro");
            Dictionary<string, object> options = new Dictionary<string, object>();
            options["baseURL"] = Api + "/api/llm/v1";
            options["apiKey"] = "{file:dayan-key}";
            Dictionary<string, object> dayan = new Dictionary<string, object>();
            dayan["npm"] = "@ai-sdk/openai-compatible";
            dayan["name"] = "Dayan";
            dayan["options"] = options;
            dayan["models"] = models;
            Section(config, "provider")["dayan"] = dayan;

            Dictionary<string, object> headers = new Dictionary<string, object>();
            headers["Authorization"] = "Bearer {file:dayan-key}";
            Dictionary<string, object> mcp = new Dictionary<string, object>();
            mcp["type"] = "remote";
            mcp["url"] = Api + "/api/mcp";
            mcp["enabled"] = true;
            mcp["oauth"] = false;
            mcp["headers"] = headers;
            // OpenCode 2 reads this 1.x form too; its timeout then also bounds each tool call.
            mcp["timeout"] = 60000;
            Section(config, "mcp")["dayan"] = mcp;

            config["model"] = "dayan/dayan";
            config["small_model"] = "dayan/dayan";
            config["default_agent"] = "dayan";

            File.WriteAllText(path, Pretty(config, 0) + "\n", new UTF8Encoding(false));
            SaveState();
        }

        // Claude Code: Dayan's AI and key in .claude\settings.json; Dayan's
        // tools through Claude Code's own command (it keeps .claude.json), the
        // header naming the key's variable, never the key.
        static void ConnectClaude(Step step)
        {
            string path = Path.Combine(ConfigDir, "settings.json");
            Dictionary<string, object> mine = AppState("claude");
            if (!mine.ContainsKey("created_config")) mine["created_config"] = !File.Exists(path);
            Dictionary<string, object> config = ReadSettings(path, step);
            Dictionary<string, object> env = Section(config, "env");
            if (!mine.ContainsKey("previous"))
            {
                RememberBefore(mine, env, ClaudeEnv);
                object agent;
                ((Dictionary<string, object>)mine["previous"])["agent"] =
                    config.TryGetValue("agent", out agent) && !LooksLikeDayan(agent) ? agent : null;
            }
            env["ANTHROPIC_BASE_URL"] = Api + "/api/llm";
            env["ANTHROPIC_AUTH_TOKEN"] = Key;
            env["DAYAN_API_KEY"] = Key;
            env["ANTHROPIC_MODEL"] = "dayan[1m]";
            env["ANTHROPIC_DEFAULT_OPUS_MODEL"] = "dayan-pro[1m]";
            env["ANTHROPIC_DEFAULT_SONNET_MODEL"] = "dayan[1m]";
            env["ANTHROPIC_DEFAULT_HAIKU_MODEL"] = "dayan";
            env["CLAUDE_CODE_SUBAGENT_MODEL"] = "dayan";
            env["DISABLE_TELEMETRY"] = "1";
            env["DISABLE_ERROR_REPORTING"] = "1";
            config["agent"] = "dayan";
            File.WriteAllText(path, Pretty(config, 0) + "\n", new UTF8Encoding(false));
            SaveState();

            RunApp("mcp remove dayan --scope user", step, false);
            RunApp("mcp add --transport http --scope user dayan \"" + Api + "/api/mcp\" "
                   + "--header \"Authorization: Bearer ${DAYAN_API_KEY}\"", step, true);
        }

        // Codex: .codex\config.toml, Dayan's lines written, every other line kept.
        static void ConnectCodex(Step step)
        {
            string path = Path.Combine(ConfigDir, "config.toml");
            string existing = File.Exists(path) ? File.ReadAllText(path).TrimStart('﻿') : "";
            Dictionary<string, object> mine = AppState("codex");
            if (!mine.ContainsKey("created_config")) mine["created_config"] = existing.Trim().Length == 0;
            if (!mine.ContainsKey("previous_lines")) CodexBefore(existing, mine);
            if (existing.Length > 0)
                File.Copy(path, path + ".before-dayan-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture), true);
            File.WriteAllText(path, CodexConfig(existing, Api, Key), new UTF8Encoding(false));
            SaveState();
        }

        // Her own top lines Dayan's replace, and whether Dayan adds the [windows]
        // table or only its sandbox line -- what a removal puts back or takes away.
        static void CodexBefore(string existing, Dictionary<string, object> mine)
        {
            List<string> lines = new List<string>();
            bool inTables = false, inWindows = false, hasWindows = false, windowsSandbox = false;
            foreach (string line in existing.Replace("\r\n", "\n").Split('\n'))
            {
                if (Regex.IsMatch(line, @"^\s*\["))
                {
                    inTables = true;
                    inWindows = Regex.IsMatch(line, @"^\s*\[\s*windows\s*\]");
                    if (inWindows) hasWindows = true;
                    continue;
                }
                if (!inTables && Regex.IsMatch(line, @"^\s*(model|model_provider|model_context_window)\s*=")
                    && !CodexDayanLine.IsMatch(line))
                    lines.Add(line);
                if (inWindows && Regex.IsMatch(line, @"^\s*sandbox\s*=")) windowsSandbox = true;
            }
            mine["previous_lines"] = lines.ToArray();
            mine["added_windows"] = !hasWindows;
            mine["added_sandbox"] = hasWindows && !windowsSandbox;
        }

        static readonly Regex CodexDayanLine = new Regex(
            @"^\s*(model\s*=\s*""dayan""|model_provider\s*=\s*""dayan""|model_context_window\s*=\s*1000000)\s*$");

        public static string CodexConfig(string existing, string api, string key)
        {
            List<string> root = new List<string>(), tables = new List<string>();
            Regex header = new Regex(@"^\s*\[");
            Regex ours = new Regex(@"^\s*\[\s*(model_providers\.dayan|mcp_servers\.dayan)(\.[^\]]*)?\s*\]");
            Regex ourKeys = new Regex(@"^\s*(model|model_provider|model_context_window)\s*=");
            Regex windows = new Regex(@"^\s*\[\s*windows\s*\]");
            bool inTables = false, skip = false, inWindows = false, hasWindows = false, windowsSandbox = false;
            int windowsHeader = -1;
            foreach (string line in existing.Replace("\r\n", "\n").Split('\n'))
            {
                if (header.IsMatch(line))
                {
                    inTables = true;
                    skip = ours.IsMatch(line);
                    inWindows = windows.IsMatch(line);
                    if (inWindows) { hasWindows = true; windowsHeader = tables.Count; }
                    if (!skip) tables.Add(line);
                    continue;
                }
                if (!inTables)
                {
                    if (!ourKeys.IsMatch(line) && !line.StartsWith("# Dayan Agent")) root.Add(line);
                }
                else if (!skip)
                {
                    if (inWindows && Regex.IsMatch(line, @"^\s*sandbox\s*=")) windowsSandbox = true;
                    tables.Add(line);
                }
            }
            // Codex on Windows writes files only once its sandbox is set up; the
            // one that needs no administrator (docs/15 §6).
            if (hasWindows && !windowsSandbox) tables.Insert(windowsHeader + 1, "sandbox = \"unelevated\"");
            while (root.Count > 0 && root[root.Count - 1].Trim().Length == 0) root.RemoveAt(root.Count - 1);
            while (tables.Count > 0 && tables[tables.Count - 1].Trim().Length == 0) tables.RemoveAt(tables.Count - 1);

            StringBuilder b = new StringBuilder();
            b.Append("# Dayan Agent: Dayan's model and tools (Run Dayan Agent on my device)\n");
            b.Append("model = \"dayan\"\nmodel_provider = \"dayan\"\nmodel_context_window = 1000000\n");
            foreach (string line in root) b.Append(line).Append('\n');
            if (tables.Count > 0) b.Append('\n');
            foreach (string line in tables) b.Append(line).Append('\n');
            if (!hasWindows) b.Append("\n[windows]\nsandbox = \"unelevated\"\n");
            b.Append("\n[model_providers.dayan]\nname = \"Dayan\"\nbase_url = \"" + api + "/api/llm/v1\"\n")
             .Append("wire_api = \"responses\"\nexperimental_bearer_token = \"" + key + "\"\n");
            b.Append("\n[mcp_servers.dayan]\nurl = \"" + api + "/api/mcp\"\n")
             .Append("http_headers = { \"Authorization\" = \"Bearer " + key + "\" }\n")
             .Append("startup_timeout_sec = 20\ntool_timeout_sec = 60\n");
            return b.ToString();
        }

        // Runs the app's own command line (never with the key on it).
        static void RunApp(string arguments, Step step, bool mustWork)
        {
            ProcessStartInfo info = new ProcessStartInfo(AppExe, arguments);
            info.UseShellExecute = false;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            info.RedirectStandardInput = true;
            info.CreateNoWindow = true;
            info.WorkingDirectory = Home;
            using (Process process = Process.Start(info))
            {
                process.StandardInput.Close();
                process.OutputDataReceived += delegate (object s, DataReceivedEventArgs e) { if (e.Data != null) Log("  " + e.Data); };
                process.ErrorDataReceived += delegate (object s, DataReceivedEventArgs e) { if (e.Data != null) Log("  " + e.Data); };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (!process.WaitForExit(120 * 1000))
                {
                    try { process.Kill(); } catch { }
                    throw new SetupFailure(AppName + " did not answer (see the log).");
                }
                if (mustWork && process.ExitCode != 0)
                    throw new SetupFailure(AppName + " didn't take Dayan's tools (see the log).");
            }
        }

        static Dictionary<string, object> Model(string name)
        {
            Dictionary<string, object> limit = new Dictionary<string, object>();
            limit["context"] = 1000000;
            limit["output"] = 32000;
            Dictionary<string, object> model = new Dictionary<string, object>();
            model["name"] = name;
            model["limit"] = limit;
            return model;
        }

        static Dictionary<string, object> Section(Dictionary<string, object> parent, string key)
        {
            object value;
            if (parent.TryGetValue(key, out value) && value is Dictionary<string, object>) return (Dictionary<string, object>)value;
            Dictionary<string, object> created = new Dictionary<string, object>();
            parent[key] = created;
            return created;
        }

        // Where the app reads Dayan's skills (and agents) from.
        static string PackRoot() { return PackRoot(App); }

        static string PackRoot(string app)
        {
            if (app == "codex") return Path.Combine(Home, ".agents");  // Codex reads its user skills from .agents\skills
            return AppConfigDir(app);
        }

        static void AddSkills(Step step)
        {
            string file = Path.Combine(Path.GetTempPath(), "DayanSetup", "dayan-pack.zip");
            Download(Api + "/api/device-setup/pack.zip?app=" + App, file, null, step, "downloading");
            string root = PackRoot();
            int count = 0;
            List<string> written = new List<string>();
            using (ZipArchive zip = ZipFile.OpenRead(file))
            {
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    string relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                    if (relative.Contains("..") || Path.IsPathRooted(relative)) continue;
                    string target = Path.Combine(root, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    entry.ExtractToFile(target, true);
                    written.Add(entry.FullName);
                    count++;
                }
            }
            TryDelete(file);
            // What a removal takes away (setups before 1.1 kept it in dayan-pack-<app>.json);
            // a later pack's files join the earlier ones.
            Dictionary<string, object> mine = AppState(App);
            List<string> all = new List<string>(Strings(mine, "pack_files"));
            foreach (string name in written) if (!all.Contains(name)) all.Add(name);
            mine["pack_root"] = root;
            mine["pack_files"] = all.ToArray();
            mine["installed_at"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            SaveState();
            step.Note = count + " files";
        }

        static void MakeWorkFolder(Step step)
        {
            Directory.CreateDirectory(WorkDir);
            step.Note = WorkDir;
            if (!PathLine) return;
            string bin = Path.GetDirectoryName(AppExe);
            if (NoRegistry) { Log("PATH not changed (test run): " + bin); return; }
            AddToUserPath(bin);
        }

        static void InstallArduino(Step step)
        {
            string bin = Path.Combine(DataDir, "bin");
            string cli = Path.Combine(bin, "arduino-cli.exe");
            if (NoInstall) { step.State = "skip"; step.Note = "skipped (test run)"; return; }
            if (!File.Exists(cli))
            {
                Dictionary<string, object> tools = GetJson(Api + "/api/device-setup/tools?os=windows&arch=" + Arch(), null);
                Dictionary<string, object> tool = tools["arduino_cli"] as Dictionary<string, object>;
                if (tool == null) throw new SetupFailure("Dayan couldn't tell which Arduino tools to install.");
                string file = Path.Combine(Path.GetTempPath(), "DayanSetup", Str(tool, "name"));
                Download(Str(tool, "url"), file, Str(tool, "sha256"), step, "downloading");
                Directory.CreateDirectory(bin);
                using (ZipArchive zip = ZipFile.OpenRead(file))
                    foreach (ZipArchiveEntry entry in zip.Entries)
                        if (entry.Name.Equals("arduino-cli.exe", StringComparison.OrdinalIgnoreCase))
                            entry.ExtractToFile(cli, true);
                TryDelete(file);
                if (!File.Exists(cli)) throw new SetupFailure("The Arduino tools could not be unpacked.");
                State["arduino_cli"] = cli;
                SaveState();
            }
            if (NoRegistry) Log("PATH not changed (test run): " + bin);
            else AddToUserPath(bin);
            // The app this setup opens at the end inherits this process's
            // PATH, not the registry's: put the folder on it as well, so Dayan
            // finds arduino-cli in that first session too.
            string processPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            if (processPath.IndexOf(bin, StringComparison.OrdinalIgnoreCase) < 0)
                Environment.SetEnvironmentVariable("PATH", bin + ";" + processPath);
            Progress(step, "getting the Arduino board files (a few minutes)…");
            RunTool(cli, "core update-index", step);
            RunTool(cli, "core install arduino:avr", step);
            step.Note = "ready";
        }

        static void RunTool(string exe, string arguments, Step step)
        {
            ProcessStartInfo info = new ProcessStartInfo(exe, arguments);
            info.UseShellExecute = false;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            info.CreateNoWindow = true;
            using (Process process = Process.Start(info))
            {
                process.OutputDataReceived += delegate (object s, DataReceivedEventArgs e) { if (e.Data != null) Log("  " + e.Data); };
                process.ErrorDataReceived += delegate (object s, DataReceivedEventArgs e) { if (e.Data != null) Log("  " + e.Data); };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                DateTime deadline = DateTime.Now.AddMinutes(20);
                while (!process.WaitForExit(250))
                {
                    if (DateTime.Now > deadline) { try { process.Kill(); } catch { } throw new SetupFailure("The Arduino download took too long."); }
                    Draw();
                }
                if (process.ExitCode != 0) throw new SetupFailure("arduino-cli " + arguments + " failed (see the log).");
            }
        }

        static bool OnUserPath(string folder)
        {
            foreach (string part in PathFolders())
                if (part.TrimEnd('\\').Equals(folder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static void AddToUserPath(string folder)
        {
            using (RegistryKey env = Registry.CurrentUser.OpenSubKey("Environment", true))
            {
                string current = (env.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string) ?? "";
                foreach (string part in current.Split(';'))
                    if (Environment.ExpandEnvironmentVariables(part).TrimEnd('\\').Equals(folder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                        return;
                string updated = current.Length == 0 ? folder : current.TrimEnd(';') + ";" + folder;
                env.SetValue("Path", updated, RegistryValueKind.ExpandString);
            }
            UIntPtr result;
            Native.SendMessageTimeout((IntPtr)0xffff, 0x001A, UIntPtr.Zero, "Environment", 0x0002, 3000, out result);
            string processPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            if (processPath.IndexOf(folder, StringComparison.OrdinalIgnoreCase) < 0)
                Environment.SetEnvironmentVariable("PATH", folder + ";" + processPath);
        }

        // Installed apps lists "Dayan Agent"; its Uninstall runs a copy of this
        // file with --uninstall (the downloaded one may be deleted by then).
        static void Register(Step step)
        {
            string copy = Path.Combine(DataDir, "DayanSetup.exe");
            string self = Assembly.GetEntryAssembly().Location;
            if (!self.Equals(copy, StringComparison.OrdinalIgnoreCase)) File.Copy(self, copy, true);
            using (RegistryKey entry = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                entry.SetValue("DisplayName", "Dayan Agent");
                entry.SetValue("DisplayVersion", BuildInfo.Version);
                entry.SetValue("Publisher", "Eques AI");
                entry.SetValue("DisplayIcon", copy + ",0");
                entry.SetValue("UninstallString", "\"" + copy + "\" --uninstall");
                entry.SetValue("InstallLocation", DataDir);
                entry.SetValue("URLInfoAbout", Api);
                entry.SetValue("NoModify", 1, RegistryValueKind.DWord);
                entry.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                entry.SetValue("EstimatedSize", (int)(new FileInfo(copy).Length / 1024) + 1, RegistryValueKind.DWord);
            }
            State["registered"] = true;
            SaveState();
            step.Note = "remove it there any time";
        }

        static string SayHello(Step step)
        {
            Dictionary<string, object> message = new Dictionary<string, object>();
            message["role"] = "user";
            message["content"] = "You have just been set up on my computer" + (FirstName.Length > 0 ? " (I'm " + FirstName + ")" : "")
                                 + ". Greet me in one short, friendly sentence.";
            Dictionary<string, object> body = new Dictionary<string, object>();
            body["model"] = "dayan";
            body["stream"] = false;
            body["max_tokens"] = 800;
            body["messages"] = new object[] { message };
            Dictionary<string, object> reply = PostJson(Api + "/api/llm/v1/chat/completions", Key, body);
            string text = "";
            object[] choices = reply.ContainsKey("choices") ? reply["choices"] as object[] : null;
            if (choices != null && choices.Length > 0)
            {
                Dictionary<string, object> first = choices[0] as Dictionary<string, object>;
                Dictionary<string, object> msg = first != null && first.ContainsKey("message") ? first["message"] as Dictionary<string, object> : null;
                if (msg != null) text = Str(msg, "content").Trim();
            }
            step.Note = "Dayan answered";
            return text;
        }

        static int Finish(string hello)
        {
            Screen.Header("All set!");
            Screen.Line(Screen.Tick + "  Dayan Agent is ready in " + AppName + ".", Screen.Green);
            Screen.Blank();
            if (!string.IsNullOrEmpty(hello))
            {
                Screen.Para("Dayan says:  " + hello, Screen.White);
                Screen.Blank();
            }
            Screen.Line("How to start:", Screen.Gold);
            string command = Path.GetFileNameWithoutExtension(AppExe);
            if (AppIsDesktop)
            {
                Screen.Line("1.  Open OpenCode (it opens when you press Enter)", Screen.White);
                Screen.Line("2.  Choose a folder for your work", Screen.White);
                Screen.Line("3.  Tell Dayan what you want to learn or build", Screen.White);
                Screen.Blank();
                Screen.Para("Tip: press Tab in OpenCode to switch between Dayan (your tutor) and Dayan Maker (projects).", Screen.Soft);
            }
            else
            {
                Screen.Line("1.  Open Terminal in your Dayan folder:  cd " + WorkDir, Screen.White);
                Screen.Line("2.  Type  " + command + "  and press Enter (it opens now when you press Enter)", Screen.White);
                Screen.Line("3.  Tell Dayan what you want to learn or build", Screen.White);
                Screen.Blank();
                if (App == "claude")
                    Screen.Para("The first time, Claude Code asks whether you trust the folder: choose Yes. Type /agents to see Dayan's helpers.", Screen.Soft);
                else if (App == "codex")
                    Screen.Para("The first time, Codex asks whether to trust the folder: choose Trust and continue.", Screen.Soft);
            }
            Screen.Blank();
            Screen.Para(NoRegistry ? "To remove Dayan Agent later, run this file with --uninstall."
                        : "To remove Dayan Agent later: Settings  ›  Apps  ›  Installed apps  ›  Dayan Agent  ›  Uninstall.",
                        Screen.Soft);
            Screen.Blank();
            if (NoOpen)
            {
                Screen.Line("Press any key to close.", Screen.Gold);
                if (!Yes) Console.ReadKey(true);
                Log("setup finished");
                return 0;
            }
            Screen.Line("Press Enter to open " + AppName + "   " + Screen.Dot + "   Esc to close", Screen.Gold);
            if (!Yes && Screen.WaitFor(ConsoleKey.Enter, ConsoleKey.Escape) == ConsoleKey.Enter)
            {
                try
                {
                    // A Terminal app opens in a new window, in the work folder.
                    ProcessStartInfo open = new ProcessStartInfo(AppExe) { UseShellExecute = true };
                    if (!AppIsDesktop) open.WorkingDirectory = WorkDir;
                    Process.Start(open);
                }
                catch (Exception error) { Log("could not open " + AppName + ": " + error.Message); }
            }
            Log("setup finished");
            return 0;
        }

        // ── The record of what the setup changed (installed.json) ─────────
        static Dictionary<string, object> LoadState()
        {
            Dictionary<string, object> state = null;
            try
            {
                if (File.Exists(StatePath))
                    state = Json.DeserializeObject(File.ReadAllText(StatePath).TrimStart('﻿')) as Dictionary<string, object>;
            }
            catch (Exception error) { Log("installed.json could not be read: " + error.Message); }
            if (state == null) state = new Dictionary<string, object>();
            if (!(state.ContainsKey("apps") && state["apps"] is Dictionary<string, object>))
                state["apps"] = new Dictionary<string, object>();
            return state;
        }

        static void SaveState()
        {
            State["version"] = BuildInfo.Version;
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(StatePath, Pretty(State, 0) + "\n", new UTF8Encoding(false));
        }

        static Dictionary<string, object> Apps() { return (Dictionary<string, object>)State["apps"]; }

        // The app's part of the record: made when the setup changes the app.
        static Dictionary<string, object> AppState(string app)
        {
            Dictionary<string, object> mine = Child(Apps(), app);
            if (mine == null) { mine = new Dictionary<string, object>(); Apps()[app] = mine; }
            return mine;
        }

        static Dictionary<string, object> Child(Dictionary<string, object> parent, string key)
        {
            object value;
            return parent != null && parent.TryGetValue(key, out value) ? value as Dictionary<string, object> : null;
        }

        static string[] Strings(Dictionary<string, object> parent, string key)
        {
            object value;
            List<string> list = new List<string>();
            if (parent != null && parent.TryGetValue(key, out value) && value is IEnumerable && !(value is string)
                && !(value is IDictionary<string, object>))
                foreach (object item in (IEnumerable)value)
                    if (item != null) list.Add(Convert.ToString(item, CultureInfo.InvariantCulture));
            return list.ToArray();
        }

        // What Dayan is about to replace, remembered on the first run only (a
        // later run would find Dayan's own values there).
        static void RememberBefore(Dictionary<string, object> mine, Dictionary<string, object> settings, string[] names)
        {
            if (mine.ContainsKey("previous")) return;
            Dictionary<string, object> previous = new Dictionary<string, object>();
            foreach (string name in names)
            {
                object value;
                previous[name] = settings != null && settings.TryGetValue(name, out value) && !LooksLikeDayan(value) ? value : null;
            }
            mine["previous"] = previous;
        }

        static bool LooksLikeDayan(object value)
        {
            string text = value as string;
            return text != null && (text.StartsWith("dayan", StringComparison.Ordinal) || text.StartsWith("dyn_", StringComparison.Ordinal)
                                    || text.EndsWith("/api/llm", StringComparison.Ordinal));
        }

        // A value this setup wrote there (the two DISABLE_ switches: "1").
        static bool SetByDayan(string name, object value)
        {
            if (name.StartsWith("DISABLE_", StringComparison.Ordinal)) return "1".Equals(value);
            return LooksLikeDayan(value);
        }

        // Dayan's value goes and hers from before comes back; a value she changed since stays.
        static void PutBack(Dictionary<string, object> settings, string name, Dictionary<string, object> previous)
        {
            object value, before;
            if (!settings.TryGetValue(name, out value) || !SetByDayan(name, value)) return;
            if (previous != null && previous.TryGetValue(name, out before) && before != null) settings[name] = before;
            else settings.Remove(name);
        }

        // A JSON settings file, or null when it is missing or unreadable (then it is never rewritten).
        static Dictionary<string, object> TryReadSettings(string path)
        {
            if (!File.Exists(path)) return null;
            try { return Json.DeserializeObject(File.ReadAllText(path).TrimStart('﻿')) as Dictionary<string, object>; }
            catch { return null; }
        }

        static string Host(string url)
        {
            try { return new Uri(url).Host; } catch { return url; }
        }

        static string Join(List<string> names)
        {
            if (names.Count < 2) return string.Join("", names.ToArray());
            return string.Join(", ", names.GetRange(0, names.Count - 1).ToArray()) + " and " + names[names.Count - 1];
        }

        // ── Removing Dayan Agent (--uninstall: what Installed apps runs) ───
        static readonly string[] AppIds = { "opencode", "claude", "codex" };

        static int Uninstall()
        {
            State = LoadState();
            List<string> apps = new List<string>();
            foreach (string app in AppIds)
                if (HasDayan(app) || Child(Apps(), app) != null || File.Exists(Path.Combine(DataDir, "dayan-pack-" + app + ".json")))
                    apps.Add(app);
            bool arduino = File.Exists(Path.Combine(DataDir, "bin", "arduino-cli.exe"));
            bool registered = Registered();
            if (apps.Count == 0 && !arduino && !registered && !Directory.Exists(DataDir))
            {
                Screen.Header("Remove Dayan Agent");
                Screen.Para("Dayan Agent isn't set up on this computer, so there is nothing to remove.", Screen.White);
                Screen.Blank();
                Screen.Line("Press any key to close.", Screen.Soft);
                Log("nothing to remove");
                if (!Yes) Console.ReadKey(true);
                return 0;
            }
            if (!ConfirmRemoval(apps, arduino, registered)) return Cancelled();

            List<Work> work = new List<Work>();
            foreach (string app in apps)
            {
                string id = app;
                Steps.Add(new Step("Removing Dayan from " + Names[id]));
                work.Add(delegate (Step step) { RemoveFromApp(id, step); });
            }
            if (arduino) { Steps.Add(new Step("Removing the Arduino tools Dayan installed")); work.Add(RemoveArduino); }
            Steps.Add(new Step("Removing Dayan Agent's own files"));
            work.Add(RemoveDayanFiles);
            for (int i = 0; i < Steps.Count; i++) Do(Steps[i], work[i]);
            return Removed();
        }

        static bool HasDayan(string app)
        {
            string dir = AppConfigDir(app);
            if (app == "codex")
            {
                string toml = Path.Combine(dir, "config.toml");
                return File.Exists(toml) && Regex.IsMatch(File.ReadAllText(toml),
                    @"^[ \t]*\[[ \t]*(model_providers|mcp_servers)\.dayan[ \t]*\]", RegexOptions.Multiline);
            }
            if (app == "claude")
            {
                Dictionary<string, object> settings = TryReadSettings(Path.Combine(dir, "settings.json"));
                Dictionary<string, object> env = Child(settings, "env");
                return "dayan".Equals(Str(settings, "agent")) || (env != null && env.ContainsKey("DAYAN_API_KEY"));
            }
            if (File.Exists(Path.Combine(dir, "dayan-key"))) return true;
            Dictionary<string, object> config = TryReadSettings(Path.Combine(dir, "opencode.json"));
            return Child(Child(config, "provider"), "dayan") != null || Child(Child(config, "mcp"), "dayan") != null;
        }

        static bool Registered()
        {
            if (NoRegistry) return false;
            try { using (RegistryKey entry = Registry.CurrentUser.OpenSubKey(UninstallKey)) return entry != null; }
            catch { return false; }
        }

        static bool ConfirmRemoval(List<string> apps, bool arduino, bool registered)
        {
            Screen.Header("Remove Dayan Agent from this computer");
            Screen.Line("Here's what I'll remove -- nothing else on this computer changes:", Screen.Gold);
            Screen.Blank();
            List<string> names = new List<string>();
            foreach (string app in apps)
            {
                names.Add(Names[app]);
                Item("Dayan in " + Names[app], "its settings, your key and the skills; yours stay as they were");
            }
            if (arduino) Item("Arduino tools", "the arduino-cli Dayan installed");
            if (registered) Item("Installed apps", "the \"Dayan Agent\" entry");
            Item("Dayan's own files", DataDir);
            Screen.Blank();
            Screen.Para("Kept: " + (names.Count > 0 ? Join(names) + (names.Count == 1 ? " itself, " : " themselves, ") : "")
                        + "your work folder (" + WorkDir + ") and the Arduino board files, which the Arduino IDE uses too.",
                        Screen.Soft);
            Screen.Blank();
            Screen.Line("Press Enter to remove   " + Screen.Dot + "   Esc to cancel", Screen.Gold);
            if (Yes) return true;
            return Screen.WaitFor(ConsoleKey.Enter, ConsoleKey.Escape) == ConsoleKey.Enter;
        }

        static void RemoveFromApp(string app, Step step)
        {
            if (app == "claude") RemoveFromClaude(step);
            else if (app == "codex") RemoveFromCodex(step);
            else RemoveFromOpenCode(step);
            int files = RemovePack(app);
            if (step.Note.Length == 0) step.Note = files > 0 ? files + " skill files" : "done";
        }

        static void RemoveFromOpenCode(Step step)
        {
            string dir = AppConfigDir("opencode");
            string path = Path.Combine(dir, "opencode.json");
            Dictionary<string, object> mine = Child(Apps(), "opencode");
            Dictionary<string, object> config = TryReadSettings(path);
            if (config != null)
            {
                RemoveChild(config, "provider", "dayan");
                RemoveChild(config, "mcp", "dayan");
                foreach (string name in OpenCodeKeys) PutBack(config, name, Child(mine, "previous"));
                WriteOrDelete(path, config, mine);
            }
            else if (File.Exists(path)) step.Note = "opencode.json could not be read: take out its \"dayan\" parts yourself";
            TryDelete(Path.Combine(dir, "dayan-key"));
        }

        static void RemoveFromClaude(Step step)
        {
            string path = Path.Combine(AppConfigDir("claude"), "settings.json");
            Dictionary<string, object> mine = Child(Apps(), "claude");
            Dictionary<string, object> previous = Child(mine, "previous");
            Dictionary<string, object> config = TryReadSettings(path);
            if (config != null)
            {
                Dictionary<string, object> env = Child(config, "env");
                if (env != null)
                {
                    foreach (string name in ClaudeEnv) PutBack(env, name, previous);
                    if (env.Count == 0) config.Remove("env");
                }
                PutBack(config, "agent", previous);
                WriteOrDelete(path, config, mine);
            }
            else if (File.Exists(path)) step.Note = "settings.json could not be read: take out Dayan's lines yourself";
            // Dayan's tools, through Claude Code's own command while Claude Code is here.
            App = "claude";
            AppName = Names[App];
            AppExe = FindApp();
            if (AppExe != null) RunApp("mcp remove dayan --scope user", step, false);
        }

        static void RemoveFromCodex(Step step)
        {
            string path = Path.Combine(AppConfigDir("codex"), "config.toml");
            if (!File.Exists(path)) return;
            Dictionary<string, object> mine = Child(Apps(), "codex");
            string text = CodexWithoutDayan(File.ReadAllText(path).TrimStart('﻿'), Strings(mine, "previous_lines"),
                                            Bool(mine, "added_windows"), Bool(mine, "added_sandbox"));
            if (text.Trim().Length == 0 && Bool(mine, "created_config")) File.Delete(path);
            else File.WriteAllText(path, text, new UTF8Encoding(false));
            // The copies an earlier run kept beside it hold a Dayan key: they go too.
            foreach (string copy in Directory.GetFiles(Path.GetDirectoryName(path), "config.toml.before-dayan-*"))
                if (File.ReadAllText(copy).Contains("experimental_bearer_token = \"dyn_")) TryDelete(copy);
        }

        // Codex's config.toml without Dayan's lines -- its comment and three top
        // keys, its two tables, the [windows] sandbox it added -- and her top
        // lines from before back in their place.
        public static string CodexWithoutDayan(string existing, string[] previous, bool addedWindows, bool addedSandbox)
        {
            List<string> root = new List<string>(), tables = new List<string>();
            Regex header = new Regex(@"^\s*\[");
            Regex ours = new Regex(@"^\s*\[\s*(model_providers\.dayan|mcp_servers\.dayan)(\.[^\]]*)?\s*\]");
            Regex windows = new Regex(@"^\s*\[\s*windows\s*\]");
            Regex sandbox = new Regex(@"^\s*sandbox\s*=\s*""unelevated""\s*$");
            bool inTables = false, skip = false, inWindows = false;
            int windowsAt = -1;
            foreach (string line in existing.Replace("\r\n", "\n").Split('\n'))
            {
                if (header.IsMatch(line))
                {
                    inTables = true;
                    skip = ours.IsMatch(line);
                    inWindows = windows.IsMatch(line);
                    if (inWindows) windowsAt = tables.Count;
                    if (!skip) tables.Add(line);
                    continue;
                }
                if (!inTables)
                {
                    if (!line.StartsWith("# Dayan Agent") && !CodexDayanLine.IsMatch(line)) root.Add(line);
                }
                else if (!skip && !(inWindows && (addedWindows || addedSandbox) && sandbox.IsMatch(line)))
                    tables.Add(line);
            }
            if (addedWindows && windowsAt >= 0)
            {
                int end = windowsAt + 1;
                while (end < tables.Count && !header.IsMatch(tables[end])) end++;
                bool empty = true;
                for (int i = windowsAt + 1; i < end; i++) if (tables[i].Trim().Length > 0) empty = false;
                if (empty) tables.RemoveRange(windowsAt, end - windowsAt);
            }
            List<string> lines = new List<string>(previous ?? new string[0]);
            lines.AddRange(root);
            TrimBlank(lines);
            TrimBlank(tables);
            if (lines.Count > 0 && tables.Count > 0) lines.Add("");
            lines.AddRange(tables);
            StringBuilder b = new StringBuilder();
            bool blank = false;
            foreach (string line in lines)
            {
                bool isBlank = line.Trim().Length == 0;
                if (isBlank && blank) continue;  // no pile of blank lines where Dayan's parts were
                blank = isBlank;
                b.Append(line).Append('\n');
            }
            return b.ToString();
        }

        static void TrimBlank(List<string> lines)
        {
            while (lines.Count > 0 && lines[0].Trim().Length == 0) lines.RemoveAt(0);
            while (lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0) lines.RemoveAt(lines.Count - 1);
        }

        static void RemoveChild(Dictionary<string, object> config, string section, string key)
        {
            Dictionary<string, object> part = Child(config, section);
            if (part == null) return;
            part.Remove(key);
            if (part.Count == 0) config.Remove(section);
        }

        // A settings file this setup made, holding nothing of hers now, goes.
        static void WriteOrDelete(string path, Dictionary<string, object> config, Dictionary<string, object> mine)
        {
            if (Bool(mine, "created_config") && (config.Count == 0 || (config.Count == 1 && config.ContainsKey("$schema"))))
            {
                File.Delete(path);
                return;
            }
            File.WriteAllText(path, Pretty(config, 0) + "\n", new UTF8Encoding(false));
        }

        // The skills the setup put there (setups before 1.1 listed them in
        // dayan-pack-<app>.json), and the folders they leave empty.
        static int RemovePack(string app)
        {
            Dictionary<string, object> mine = Child(Apps(), app);
            string root = Str(mine, "pack_root");
            string[] files = Strings(mine, "pack_files");
            Dictionary<string, object> legacy = TryReadSettings(Path.Combine(DataDir, "dayan-pack-" + app + ".json"));
            if (files.Length == 0 && legacy != null)
            {
                root = Str(legacy, "root");
                files = Strings(legacy, "files");
            }
            if (root.Length == 0) root = PackRoot(app);
            string top = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            int removed = 0;
            foreach (string name in files)
            {
                string relative = name.Replace('/', Path.DirectorySeparatorChar);
                if (relative.Contains("..") || Path.IsPathRooted(relative)) continue;
                string path = Path.Combine(top, relative);
                if (File.Exists(path)) { File.Delete(path); removed++; }
                string folder = Path.GetDirectoryName(path);
                while (folder != null && folder.Length > top.Length && Directory.Exists(folder)
                       && Directory.GetFileSystemEntries(folder).Length == 0)
                {
                    Directory.Delete(folder);
                    folder = Path.GetDirectoryName(folder);
                }
            }
            return removed;
        }

        static void RemoveArduino(Step step)
        {
            string bin = Path.Combine(DataDir, "bin");
            TryDelete(Path.Combine(bin, "arduino-cli.exe"));
            if (!NoRegistry) RemoveFromUserPath(bin);
            step.Note = "the board files stay: the Arduino IDE uses them too";
        }

        static void RemoveDayanFiles(Step step)
        {
            if (!NoRegistry)
            {
                RemoveFromUserPath(Path.Combine(DataDir, "bin"));
                try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); }
                catch (Exception error) { Log("the Installed apps entry could not be removed: " + error.Message); }
            }
            string folder = Path.GetFullPath(DataDir).TrimEnd(Path.DirectorySeparatorChar);
            step.Note = "done";
            if (!Directory.Exists(folder)) return;
            string self = Path.GetFullPath(Assembly.GetEntryAssembly().Location);
            if (!self.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                Directory.Delete(folder, true);
                return;
            }
            // Run from that folder (Installed apps): the rest goes now, the folder
            // itself a moment after this window closes.
            foreach (string entry in Directory.GetFileSystemEntries(folder))
                if (!entry.Equals(self, StringComparison.OrdinalIgnoreCase))
                {
                    try { if (Directory.Exists(entry)) Directory.Delete(entry, true); else File.Delete(entry); }
                    catch (Exception error) { Log("could not remove " + entry + ": " + error.Message); }
                }
            ProcessStartInfo later = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                "/d /c ping -n 4 127.0.0.1 >nul & rd /s /q \"" + folder + "\"");
            later.UseShellExecute = false;
            later.CreateNoWindow = true;
            Process.Start(later);
        }

        static void RemoveFromUserPath(string folder)
        {
            using (RegistryKey env = Registry.CurrentUser.OpenSubKey("Environment", true))
            {
                if (env == null) return;
                string current = (env.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string) ?? "";
                List<string> kept = new List<string>();
                bool changed = false;
                foreach (string part in current.Split(';'))
                {
                    if (part.Length > 0 && Environment.ExpandEnvironmentVariables(part).TrimEnd('\\')
                        .Equals(folder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) { changed = true; continue; }
                    kept.Add(part);
                }
                if (!changed) return;
                env.SetValue("Path", string.Join(";", kept.ToArray()), RegistryValueKind.ExpandString);
            }
            UIntPtr result;
            Native.SendMessageTimeout((IntPtr)0xffff, 0x001A, UIntPtr.Zero, "Environment", 0x0002, 3000, out result);
        }

        static int Removed()
        {
            Log("removal finished");
            Screen.Header("Dayan Agent was removed");
            Screen.Line(Screen.Tick + "  Dayan Agent is no longer set up on this computer.", Screen.Green);
            Screen.Blank();
            Screen.Para("Your Dayan key keeps working until you revoke it: in Dayan, Settings  ›  Device keys.", Screen.White);
            Screen.Blank();
            Screen.Line("Press any key to close.", Screen.Soft);
            if (!Yes) Console.ReadKey(true);
            return 0;
        }

        // ── HTTP ───────────────────────────────────────────────────────────
        static HttpWebRequest Request(string url, string method, string key)
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)12288;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = method;
            request.UserAgent = "DayanSetup/" + BuildInfo.Version + " (Windows)";
            request.Timeout = 60000;
            request.ReadWriteTimeout = 120000;
            if (key != null) request.Headers["Authorization"] = "Bearer " + key;
            return request;
        }

        static Dictionary<string, object> GetJson(string url, string key)
        {
            HttpWebRequest request = Request(url, "GET", key);
            request.Accept = "application/json";
            using (WebResponse response = request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                return Json.DeserializeObject(reader.ReadToEnd()) as Dictionary<string, object> ?? new Dictionary<string, object>();
        }

        static Dictionary<string, object> PostJson(string url, string key, Dictionary<string, object> body)
        {
            HttpWebRequest request = Request(url, "POST", key);
            request.ContentType = "application/json";
            request.Accept = "application/json";
            byte[] data = new UTF8Encoding(false).GetBytes(Json.Serialize(body));
            request.ContentLength = data.Length;
            using (Stream stream = request.GetRequestStream()) stream.Write(data, 0, data.Length);
            using (WebResponse response = request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                return Json.DeserializeObject(reader.ReadToEnd()) as Dictionary<string, object> ?? new Dictionary<string, object>();
        }

        static void Download(string url, string path, string sha256, Step step, string label)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            HttpWebRequest request = Request(url, "GET", null);
            using (WebResponse response = request.GetResponse())
            using (Stream input = response.GetResponseStream())
            using (FileStream output = File.Create(path))
            {
                long total = response.ContentLength, done = 0;
                byte[] buffer = new byte[81920];
                DateTime shown = DateTime.MinValue;
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                    done += read;
                    if ((DateTime.Now - shown).TotalMilliseconds > 200)
                    {
                        shown = DateTime.Now;
                        Progress(step, total > 0 ? string.Format("{0} {1}%", label, done * 100 / total)
                                                 : string.Format("{0} {1:0.0} MB", label, done / 1048576.0));
                    }
                }
            }
            if (!string.IsNullOrEmpty(sha256))
            {
                string actual;
                using (SHA256 hasher = SHA256.Create())
                using (FileStream stream = File.OpenRead(path))
                    actual = BitConverter.ToString(hasher.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (!actual.Equals(sha256.ToLowerInvariant()))
                {
                    TryDelete(path);
                    throw new SetupFailure("The download was damaged (checksum mismatch). Try again.");
                }
            }
        }

        static int Status(WebException error)
        {
            HttpWebResponse response = error.Response as HttpWebResponse;
            return response == null ? 0 : (int)response.StatusCode;
        }

        static string Arch()
        {
            // x64 everywhere: Windows on Arm runs x64 apps, and the Arduino
            // tools publish x64 Windows files only.
            return "x64";
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        static string Str(Dictionary<string, object> data, string key)
        {
            object value;
            return data != null && data.TryGetValue(key, out value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : "";
        }

        static bool Bool(Dictionary<string, object> data, string key)
        {
            object value;
            return data != null && data.TryGetValue(key, out value) && value is bool && (bool)value;
        }

        // ── JSON, written readably (JavaScriptSerializer writes one line) ──
        static string Pretty(object value, int depth)
        {
            string pad = new string(' ', depth * 2), inner = new string(' ', (depth + 1) * 2);
            if (value == null) return "null";
            if (value is string) return Quote((string)value);
            if (value is bool) return (bool)value ? "true" : "false";
            if (value is IDictionary<string, object>)
            {
                IDictionary<string, object> map = (IDictionary<string, object>)value;
                if (map.Count == 0) return "{}";
                List<string> items = new List<string>();
                foreach (KeyValuePair<string, object> pair in map)
                    items.Add(inner + Quote(pair.Key) + ": " + Pretty(pair.Value, depth + 1));
                return "{\n" + string.Join(",\n", items.ToArray()) + "\n" + pad + "}";
            }
            if (value is IEnumerable)
            {
                List<string> items = new List<string>();
                foreach (object item in (IEnumerable)value) items.Add(inner + Pretty(item, depth + 1));
                if (items.Count == 0) return "[]";
                return "[\n" + string.Join(",\n", items.ToArray()) + "\n" + pad + "]";
            }
            if (value is IFormattable) return ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture);
            return Quote(value.ToString());
        }

        static string Quote(string text)
        {
            StringBuilder builder = new StringBuilder("\"");
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < ' ') builder.Append("\\u").Append(((int)c).ToString("x4"));
                        else builder.Append(c);
                        break;
                }
            }
            return builder.Append('"').ToString();
        }
    }
}
