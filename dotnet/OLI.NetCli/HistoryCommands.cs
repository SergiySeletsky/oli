using System;
using System.CommandLine;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

public static class HistoryCommands
{
    public static string HistoryPath => Path.Combine(AppContext.BaseDirectory, "history.jsonl");

    public static void AppendHistory(IEnumerable<string> lines)
    {
        using var sw = new StreamWriter(HistoryPath, append: true);
        foreach (var line in lines)
        {
            var record = new { time = DateTime.UtcNow, content = line };
            sw.WriteLine(JsonSerializer.Serialize(record));
        }
    }

    public static void Register(RootCommand root)
    {
        var historyPathCmd = new Command("history-path", "Show path to conversation history");
        historyPathCmd.SetHandler(() =>
        {
            Console.WriteLine(HistoryPath);
            return Task.CompletedTask;
        });

        var historyExistsCmd = new Command("history-exists", "Check if conversation history file exists");
        historyExistsCmd.SetHandler(() =>
        {
            Console.WriteLine(File.Exists(HistoryPath) ? "true" : "false");
            return Task.CompletedTask;
        });

        var historyCountCmd = new Command("history-count", "Show number of history entries");
        historyCountCmd.SetHandler(() =>
        {
            int count = File.Exists(HistoryPath) ? File.ReadAllLines(HistoryPath).Length : 0;
            Console.WriteLine(count);
            return Task.CompletedTask;
        });

        var showLinesOpt = new Option<int>("--lines", () => 20);
        var showHistoryCmd = new Command("show-history", "Display history lines") { showLinesOpt };
        showHistoryCmd.SetHandler((int lines) =>
        {
            if (!File.Exists(HistoryPath)) return Task.CompletedTask;
            foreach (var line in File.ReadLines(HistoryPath).TakeLast(lines))
                Console.WriteLine(line);
            return Task.CompletedTask;
        }, showLinesOpt);

        var clearHistoryFileCmd = new Command("delete-history", "Delete conversation history file");
        clearHistoryFileCmd.SetHandler(() =>
        {
            if (File.Exists(HistoryPath)) File.Delete(HistoryPath);
            return Task.CompletedTask;
        });

        var exportPathArg = new Argument<string>("path");
        var exportHistoryCmd = new Command("export-history", "Copy history file") { exportPathArg };
        exportHistoryCmd.SetHandler((string path) =>
        {
            if (File.Exists(HistoryPath)) File.Copy(HistoryPath, path, overwrite: true);
            Console.WriteLine($"exported to {path}");
            return Task.CompletedTask;
        }, exportPathArg);

        var importPathArg = new Argument<string>("path");
        var importHistoryCmd = new Command("import-history", "Append entries from file") { importPathArg };
        importHistoryCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("file not found"); return; }
            var lines = await File.ReadAllLinesAsync(path);
            await File.AppendAllLinesAsync(HistoryPath, lines);
            Console.WriteLine("imported");
        }, importPathArg);

        var archiveHistoryCmd = new Command("archive-history", "Archive current history to timestamped file");
        archiveHistoryCmd.SetHandler(() =>
        {
            if (!File.Exists(HistoryPath)) return Task.CompletedTask;
            var ts = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var dest = Path.Combine(Path.GetDirectoryName(HistoryPath)!, $"history-{ts}.jsonl");
            File.Move(HistoryPath, dest);
            Console.WriteLine(dest);
            return Task.CompletedTask;
        });

        var compOutOpt = new Option<string>("--out", () => "history.jsonl.gz");
        var compressHistoryCmd = new Command("compress-history", "Gzip history file") { compOutOpt };
        compressHistoryCmd.SetHandler((string outPath) =>
        {
            if (!File.Exists(HistoryPath)) return Task.CompletedTask;
            using var fs = new FileStream(HistoryPath, FileMode.Open, FileAccess.Read);
            using var outFs = new FileStream(outPath, FileMode.Create, FileAccess.Write);
            using var gz = new GZipStream(outFs, CompressionLevel.Optimal);
            fs.CopyTo(gz);
            Console.WriteLine(outPath);
            return Task.CompletedTask;
        }, compOutOpt);

        var decompPathArg = new Argument<string>("path");
        var decompressHistoryCmd = new Command("decompress-history", "Ungzip to history file") { decompPathArg };
        decompressHistoryCmd.SetHandler((string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("file not found"); return Task.CompletedTask; }
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var gz = new GZipStream(fs, CompressionMode.Decompress);
            using var outFs = new FileStream(HistoryPath, FileMode.Create, FileAccess.Write);
            gz.CopyTo(outFs);
            Console.WriteLine("decompressed");
            return Task.CompletedTask;
        }, decompPathArg);

        root.Add(historyPathCmd);
        root.Add(historyExistsCmd);
        root.Add(historyCountCmd);
        root.Add(showHistoryCmd);
        root.Add(clearHistoryFileCmd);
        root.Add(exportHistoryCmd);
        root.Add(importHistoryCmd);
        root.Add(archiveHistoryCmd);
        root.Add(compressHistoryCmd);
        root.Add(decompressHistoryCmd);
    }
}
