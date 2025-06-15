using System;
using System.CommandLine;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

record HistoryRecord
{
    public DateTime time { get; set; }
    public string content { get; set; } = "";
}

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

        var firstOpt = new Option<int>("--count", () => 10);
        var historyFirstCmd = new Command("history-first", "Show first N entries") { firstOpt };
        historyFirstCmd.SetHandler((int count) =>
        {
            if (!File.Exists(HistoryPath)) return Task.CompletedTask;
            foreach (var line in File.ReadLines(HistoryPath).Take(count))
                Console.WriteLine(line);
            return Task.CompletedTask;
        }, firstOpt);

        var lastOpt = new Option<int>("--count", () => 10);
        var historyLastCmd = new Command("history-last", "Show last N entries") { lastOpt };
        historyLastCmd.SetHandler((int count) =>
        {
            if (!File.Exists(HistoryPath)) return Task.CompletedTask;
            foreach (var line in File.ReadLines(HistoryPath).TakeLast(count))
                Console.WriteLine(line);
            return Task.CompletedTask;
        }, lastOpt);

        var rangeStartArg = new Argument<int>("start");
        var rangeEndArg = new Argument<int>("end");
        var historyRangeCmd = new Command("history-range", "Show history lines in range") { rangeStartArg, rangeEndArg };
        historyRangeCmd.SetHandler((int start, int end) =>
        {
            if (!File.Exists(HistoryPath)) return Task.CompletedTask;
            int index = 0;
            foreach (var line in File.ReadLines(HistoryPath))
            {
                if (index >= start && index <= end) Console.WriteLine(line);
                if (index > end) break;
                index++;
            }
            return Task.CompletedTask;
        }, rangeStartArg, rangeEndArg);

        var searchArg = new Argument<string>("text");
        var historySearchCmd = new Command("history-search", "Search history for text") { searchArg };
        historySearchCmd.SetHandler((string text) =>
        {
            if (!File.Exists(HistoryPath)) return Task.CompletedTask;
            foreach (var (line, i) in File.ReadLines(HistoryPath).Select((l, i) => (l, i)))
            {
                if (line.Contains(text, StringComparison.OrdinalIgnoreCase))
                    Console.WriteLine($"{i}: {line}");
            }
            return Task.CompletedTask;
        }, searchArg);

        var statsCmd = new Command("history-stats", "Show history entry statistics");
        statsCmd.SetHandler(() =>
        {
            if (!File.Exists(HistoryPath)) { Console.WriteLine("none"); return Task.CompletedTask; }
            var lines = File.ReadAllLines(HistoryPath);
            int chars = lines.Sum(l => l.Length);
            Console.WriteLine($"Entries:{lines.Length} Chars:{chars}");
            return Task.CompletedTask;
        });

        var beforeArg = new Argument<DateTime>("before");
        var deleteBeforeCmd = new Command("history-delete-before", "Delete entries before timestamp") { beforeArg };
        deleteBeforeCmd.SetHandler((DateTime before) =>
        {
            if (!File.Exists(HistoryPath)) return Task.CompletedTask;
            var kept = new List<string>();
            foreach (var line in File.ReadLines(HistoryPath))
            {
                try
                {
                    var rec = JsonSerializer.Deserialize<HistoryRecord>(line);
                    if (rec != null && rec.time >= before) kept.Add(line);
                }
                catch { }
            }
            File.WriteAllLines(HistoryPath, kept);
            Console.WriteLine("trimmed");
            return Task.CompletedTask;
        }, beforeArg);

        var truncOpt = new Option<int>("--keep", () => 1000);
        var historyTruncateCmd = new Command("history-truncate", "Keep only latest N entries") { truncOpt };
        historyTruncateCmd.SetHandler((int keep) =>
        {
            if (!File.Exists(HistoryPath)) return Task.CompletedTask;
            var lines = File.ReadLines(HistoryPath).TakeLast(keep).ToArray();
            File.WriteAllLines(HistoryPath, lines);
            Console.WriteLine("truncated");
            return Task.CompletedTask;
        }, truncOpt);

        var exportCsvArg = new Argument<string>("path");
        var exportCsvCmd = new Command("export-history-csv", "Export history to CSV") { exportCsvArg };
        exportCsvCmd.SetHandler((string path) =>
        {
            if (!File.Exists(HistoryPath)) return Task.CompletedTask;
            using var writer = new StreamWriter(path);
            writer.WriteLine("time,content");
            foreach (var line in File.ReadLines(HistoryPath))
            {
                var rec = JsonSerializer.Deserialize<HistoryRecord>(line);
                if (rec != null)
                    writer.WriteLine($"{rec.time:o},{EscapeCsv(rec.content)}");
            }
            Console.WriteLine($"exported to {path}");
            return Task.CompletedTask;
        }, exportCsvArg);

        var importCsvArg = new Argument<string>("path");
        var importCsvCmd = new Command("import-history-csv", "Import history from CSV") { importCsvArg };
        importCsvCmd.SetHandler((string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("file not found"); return Task.CompletedTask; }
            var lines = File.ReadLines(path).Skip(1);
            foreach (var line in lines)
            {
                var parts = SplitCsv(line);
                if (parts.Length < 2) continue;
                var rec = new HistoryRecord { time = DateTime.Parse(parts[0]), content = parts[1] };
                File.AppendAllText(HistoryPath, JsonSerializer.Serialize(rec) + "\n");
            }
            Console.WriteLine("imported");
            return Task.CompletedTask;
        }, importCsvArg);

        var summaryCmd = new Command("history-summary", "Summarize history text");
        summaryCmd.SetHandler(async () =>
        {
            if (!File.Exists(HistoryPath)) { Console.WriteLine("none"); return; }
            var text = string.Join(" ", File.ReadAllLines(HistoryPath));
            var summary = await KernelUtils.SummarizeAsync(text);
            Console.WriteLine(summary);
        });

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
        root.Add(historyFirstCmd);
        root.Add(historyLastCmd);
        root.Add(historyRangeCmd);
        root.Add(historySearchCmd);
        root.Add(statsCmd);
        root.Add(deleteBeforeCmd);
        root.Add(historyTruncateCmd);
        root.Add(exportCsvCmd);
        root.Add(importCsvCmd);
        root.Add(summaryCmd);
    }

    static string EscapeCsv(string s)
    {
        return s.Contains(',') || s.Contains('"') || s.Contains('\n')
            ? "\"" + s.Replace("\"", "\"\"") + "\""
            : s;
    }

    static string[] SplitCsv(string line)
    {
        var parts = new List<string>();
        var sb = new System.Text.StringBuilder();
        bool quote = false;
        foreach (var c in line)
        {
            if (c == '"') { quote = !quote; continue; }
            if (c == ',' && !quote)
            {
                parts.Add(sb.ToString());
                sb.Clear();
                continue;
            }
            sb.Append(c);
        }
        parts.Add(sb.ToString());
        return parts.ToArray();
    }
}
