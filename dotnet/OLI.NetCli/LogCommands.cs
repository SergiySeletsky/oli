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

        var trimLinesOpt = new Option<int>("--lines", () => 1000);
        var trimLogCmd = new Command("trim-log", "Keep only last N lines of log") { trimLinesOpt };
        trimLogCmd.SetHandler((int lines) =>
        {
            if (!File.Exists(LogPath)) return Task.CompletedTask;
            var all = File.ReadAllLines(LogPath).TakeLast(lines);
            File.WriteAllLines(LogPath, all);
            return Task.CompletedTask;
        }, trimLinesOpt);

        root.Add(openCmd);
        root.Add(rotateCmd);
        root.Add(sizeCmd);
        root.Add(showLevelCmd);
        root.Add(searchRegexCmd);
        root.Add(tailLogCmd);
        root.Add(trimLogCmd);

        // compress-log
        var compressCmd = new Command("compress-log", "Compress log to gz file");
        var compOutOpt = new Option<string>("--out", () => "app.log.gz");
        compressCmd.AddOption(compOutOpt);
        compressCmd.SetHandler((string outPath) =>
        {
            if (!File.Exists(LogPath)) { Console.WriteLine("no log"); return Task.CompletedTask; }
            using var fs = new FileStream(LogPath, FileMode.Open, FileAccess.Read);
            using var outFs = new FileStream(outPath, FileMode.Create, FileAccess.Write);
            using var gz = new System.IO.Compression.GZipStream(outFs, System.IO.Compression.CompressionLevel.Optimal);
            fs.CopyTo(gz);
            Console.WriteLine($"compressed to {outPath}");
            return Task.CompletedTask;
        }, compOutOpt);

        // split-log
        var splitLinesOpt = new Option<int>("--lines", () => 10000);
        var splitCmd = new Command("split-log", "Split log into numbered files") { splitLinesOpt };
        splitCmd.SetHandler((int lines) =>
        {
            if (!File.Exists(LogPath)) { Console.WriteLine("no log"); return Task.CompletedTask; }
            var all = File.ReadAllLines(LogPath);
            int file = 0;
            for (int i = 0; i < all.Length; i += lines)
            {
                var chunk = all.Skip(i).Take(lines);
                var path = Path.Combine(Path.GetDirectoryName(LogPath)!, $"app-{file}.log");
                File.WriteAllLines(path, chunk);
                Console.WriteLine(path);
                file++;
            }
            return Task.CompletedTask;
        }, splitLinesOpt);

        var exportJsonCmd = new Command("export-log-json", "Export log as JSON array") { new Option<string>("--out", () => "app.log.json") };
        exportJsonCmd.SetHandler((string outPath) =>
        {
            if (!File.Exists(LogPath)) { Console.WriteLine("no log"); return Task.CompletedTask; }
            var lines = File.ReadAllLines(LogPath);
            File.WriteAllText(outPath, System.Text.Json.JsonSerializer.Serialize(lines));
            Console.WriteLine($"exported to {outPath}");
            return Task.CompletedTask;
        }, exportJsonCmd.Options.OfType<Option<string>>().First());

        root.Add(compressCmd);
        root.Add(splitCmd);
        root.Add(exportJsonCmd);
    }
}
