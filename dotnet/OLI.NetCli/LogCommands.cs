using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

public static class LogCommands
{
    public static string LogPath => Path.Combine(AppContext.BaseDirectory, "app.log");

    public static void Register(RootCommand root)
    {
        // open-log
        var openCmd = new Command("open-log", "Open log file in default viewer");
        openCmd.SetHandler(() => { Process.Start("xdg-open", LogPath); return Task.CompletedTask; });

        // rotate-log
        var rotateCmd = new Command("rotate-log", "Rotate log file");
        rotateCmd.SetHandler(() =>
        {
            if (File.Exists(LogPath))
            {
                var ts = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                var dest = Path.Combine(Path.GetDirectoryName(LogPath)!, $"app-{ts}.log");
                File.Move(LogPath, dest);
                Console.WriteLine($"Rotated to {dest}");
            }
            return Task.CompletedTask;
        });

        // log-size
        var sizeCmd = new Command("log-size", "Display log file size");
        sizeCmd.SetHandler(() =>
        {
            if (File.Exists(LogPath))
            {
                var len = new FileInfo(LogPath).Length;
                Console.WriteLine(len);
            }
            else Console.WriteLine("0");
            return Task.CompletedTask;
        });

        var showLevelCmd = new Command("show-log-level", "Display current log level");
        showLevelCmd.SetHandler(() =>
        {
            var state = Program.LoadState();
            Console.WriteLine(state.LogLevel);
            return Task.CompletedTask;
        });

        var regexArg = new Argument<string>("pattern");
        var searchRegexCmd = new Command("search-log-regex", "Regex search log file") { regexArg };
        searchRegexCmd.SetHandler((string pattern) =>
        {
            if (!File.Exists(LogPath)) return Task.CompletedTask;
            var regex = new System.Text.RegularExpressions.Regex(pattern);
            foreach (var line in File.ReadLines(LogPath))
                if (regex.IsMatch(line)) Console.WriteLine(line);
            return Task.CompletedTask;
        }, regexArg);

        var tailLinesOpt = new Option<int>("--lines", () => 10);
        var tailFollowOpt = new Option<bool>("--follow", () => false);
        var tailLogCmd = new Command("tail-log", "Tail log file") { tailLinesOpt, tailFollowOpt };
        tailLogCmd.SetHandler(async (int lines, bool follow) =>
        {
            if (!File.Exists(LogPath)) { Console.WriteLine("no log"); return; }
            long offset = Math.Max(0, new FileInfo(LogPath).Length - 4096);
            using var fs = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(offset, SeekOrigin.Begin);
            using var sr = new StreamReader(fs);
            var all = await sr.ReadToEndAsync();
            var arr = all.Split('\n').TakeLast(lines);
            foreach (var l in arr) Console.WriteLine(l);
            if (follow)
            {
                while (true)
                {
                    var line = await sr.ReadLineAsync();
                    if (line != null) Console.WriteLine(line);
                    await Task.Delay(1000);
                }
            }
        }, tailLinesOpt, tailFollowOpt);

        root.Add(openCmd);
        root.Add(rotateCmd);
        root.Add(sizeCmd);
        root.Add(showLevelCmd);
        root.Add(searchRegexCmd);
        root.Add(tailLogCmd);
    }
}
