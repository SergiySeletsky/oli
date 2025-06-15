using System;
using System.CommandLine;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

public static class SummaryCommands
{
    public static void Register(RootCommand root)
    {
        var showSummariesCmd = new Command("show-summaries", "Display conversation summaries");
        showSummariesCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            if (state.ConversationSummaries.Count == 0)
            {
                Console.WriteLine("No summaries");
            }
            for (int i = 0; i < state.ConversationSummaries.Count; i++)
            {
                var s = state.ConversationSummaries[i];
                Console.WriteLine($"[{i}] {s.Content} ({s.MessagesCount} msgs, {s.OriginalChars} chars)");
            }
            await Task.CompletedTask;
        });

        var exportSummariesPathOpt = new Option<string>("--path") { IsRequired = true };
        var exportSummariesCmd = new Command("export-summaries", "Export summaries to file")
        {
            exportSummariesPathOpt
        };
        exportSummariesCmd.SetHandler(async (string path) =>
        {
            var state = Program.LoadState();
            File.WriteAllText(path, JsonSerializer.Serialize(state.ConversationSummaries, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Summaries exported to {path}");
            await Task.CompletedTask;
        }, exportSummariesPathOpt);

        var importSummariesPathOpt = new Option<string>("--path") { IsRequired = true };
        var importSummariesCmd = new Command("import-summaries", "Load summaries from file")
        {
            importSummariesPathOpt
        };
        importSummariesCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return;
            }
            var json = File.ReadAllText(path);
            var summaries = JsonSerializer.Deserialize<System.Collections.Generic.List<ConversationSummary>>(json);
            if (summaries != null)
            {
                var state = Program.LoadState();
                state.ConversationSummaries = summaries;
                Program.SaveState(state);
                Console.WriteLine("Summaries imported");
            }
            else
            {
                Console.WriteLine("Invalid summaries file");
            }
            await Task.CompletedTask;
        }, importSummariesPathOpt);

        var clearSummariesCmd = new Command("clear-summaries", "Remove all conversation summaries");
        clearSummariesCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            state.ConversationSummaries.Clear();
            Program.SaveState(state);
            Console.WriteLine("Summaries cleared");
            await Task.CompletedTask;
        });

        var deleteSummaryIndexOpt = new Option<int>("--index") { IsRequired = true };
        var deleteSummaryCmd = new Command("delete-summary", "Remove a summary by index") { deleteSummaryIndexOpt };
        deleteSummaryCmd.SetHandler(async (int index) =>
        {
            var state = Program.LoadState();
            if (index >= 0 && index < state.ConversationSummaries.Count)
            {
                state.ConversationSummaries.RemoveAt(index);
                Program.SaveState(state);
                Console.WriteLine("Summary deleted");
            }
            else
            {
                Console.WriteLine("Invalid index");
            }
            await Task.CompletedTask;
        }, deleteSummaryIndexOpt);

        var startRangeOpt = new Option<int>("--start") { IsRequired = true };
        var endRangeOpt = new Option<int>("--end") { IsRequired = true };
        var deleteSummaryRangeCmd = new Command("delete-summary-range", "Delete summaries in range") { startRangeOpt, endRangeOpt };
        deleteSummaryRangeCmd.SetHandler(async (int start, int end) =>
        {
            var state = Program.LoadState();
            if (start >= 0 && end >= start && end < state.ConversationSummaries.Count)
            {
                state.ConversationSummaries.RemoveRange(start, end - start + 1);
                Program.SaveState(state);
                Console.WriteLine("Summaries removed");
            }
            else
            {
                Console.WriteLine("Invalid range");
            }
            await Task.CompletedTask;
        }, startRangeOpt, endRangeOpt);

        var latestSummaryCmd = new Command("latest-summary", "Show the latest summary");
        latestSummaryCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            if (state.ConversationSummaries.Count == 0)
            {
                Console.WriteLine("none");
                await Task.CompletedTask;
                return;
            }
            var last = state.ConversationSummaries[^1];
            Console.WriteLine(last.Content);
            await Task.CompletedTask;
        });

        var summaryInfoIndexOpt = new Option<int>("--index") { IsRequired = true };
        var summaryInfoCmd = new Command("summary-info", "Show summary details") { summaryInfoIndexOpt };
        summaryInfoCmd.SetHandler(async (int index) =>
        {
            var state = Program.LoadState();
            if (index >= 0 && index < state.ConversationSummaries.Count)
            {
                var s = state.ConversationSummaries[index];
                Console.WriteLine(JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.WriteLine("Invalid index");
            }
            await Task.CompletedTask;
        }, summaryInfoIndexOpt);

        var summaryExistsCmd = new Command("summary-exists", "Check if summary index exists") { summaryInfoIndexOpt };
        summaryExistsCmd.SetHandler(async (int index) =>
        {
            var state = Program.LoadState();
            Console.WriteLine(index >= 0 && index < state.ConversationSummaries.Count ? "true" : "false");
            await Task.CompletedTask;
        }, summaryInfoIndexOpt);

        var appendSummaryTextOpt = new Option<string>("--text") { IsRequired = true };
        var appendSummaryCmd = new Command("append-summary", "Append text to a summary") { summaryInfoIndexOpt, appendSummaryTextOpt };
        appendSummaryCmd.SetHandler(async (int index, string text) =>
        {
            var state = Program.LoadState();
            if (index >= 0 && index < state.ConversationSummaries.Count)
            {
                state.ConversationSummaries[index].Content += " " + text;
                Program.SaveState(state);
                Console.WriteLine("updated");
            }
            else
            {
                Console.WriteLine("Invalid index");
            }
            await Task.CompletedTask;
        }, summaryInfoIndexOpt, appendSummaryTextOpt);

        var summaryCountCmd = new Command("summary-count", "Show number of conversation summaries");
        summaryCountCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            Console.WriteLine(state.ConversationSummaries.Count);
            await Task.CompletedTask;
        });

        var summaryAgeIndexOpt = new Option<int>("--index") { IsRequired = true };
        var summaryAgeCmd = new Command("summary-age", "Age in seconds of a summary") { summaryAgeIndexOpt };
        summaryAgeCmd.SetHandler(async (int index) =>
        {
            var state = Program.LoadState();
            if (index < 0 || index >= state.ConversationSummaries.Count) { Console.WriteLine("invalid index"); return; }
            var s = state.ConversationSummaries[index];
            var age = (DateTime.UtcNow - s.CreatedAt).TotalSeconds;
            Console.WriteLine(age.ToString("F0"));
            await Task.CompletedTask;
        }, summaryAgeIndexOpt);

        var rangeStartArg = new Argument<int>("start");
        var rangeEndArg = new Argument<int>("end");
        var summaryRangeCmd = new Command("summary-range", "Show summaries in range") { rangeStartArg, rangeEndArg };
        summaryRangeCmd.SetHandler(async (int start, int end) =>
        {
            var state = Program.LoadState();
            if (start < 0 || end < start || end >= state.ConversationSummaries.Count) { Console.WriteLine("invalid range"); return; }
            for (int i = start; i <= end; i++)
            {
                var s = state.ConversationSummaries[i];
                Console.WriteLine($"[{i}] {s.Content}");
            }
            await Task.CompletedTask;
        }, rangeStartArg, rangeEndArg);

        var exportSumIndexArg = new Argument<int>("index");
        var exportSumPathArg = new Argument<string>("path");
        var exportSummaryMdCmd = new Command("export-summary-md", "Export summary to markdown") { exportSumIndexArg, exportSumPathArg };
        exportSummaryMdCmd.SetHandler(async (int index, string path) =>
        {
            var state = Program.LoadState();
            if (index < 0 || index >= state.ConversationSummaries.Count) { Console.WriteLine("invalid index"); return; }
            await File.WriteAllTextAsync(path, state.ConversationSummaries[index].Content);
            Console.WriteLine($"exported to {path}");
        }, exportSumIndexArg, exportSumPathArg);

        var importSumPathArg = new Argument<string>("path");
        var importSummaryMdCmd = new Command("import-summary-md", "Add summary from markdown file") { importSumPathArg };
        importSummaryMdCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("file not found"); return; }
            var text = await File.ReadAllTextAsync(path);
            var state = Program.LoadState();
            state.ConversationSummaries.Add(new ConversationSummary
            {
                Content = text,
                CreatedAt = DateTime.UtcNow,
                MessagesCount = 0,
                OriginalChars = text.Length
            });
            Program.SaveState(state);
            Console.WriteLine("imported");
            await Task.CompletedTask;
        }, importSumPathArg);

        root.Add(showSummariesCmd);
        root.Add(exportSummariesCmd);
        root.Add(importSummariesCmd);
        root.Add(deleteSummaryCmd);
        root.Add(deleteSummaryRangeCmd);
        root.Add(latestSummaryCmd);
        root.Add(clearSummariesCmd);
        root.Add(summaryInfoCmd);
        root.Add(summaryExistsCmd);
        root.Add(appendSummaryCmd);
        root.Add(summaryCountCmd);
        root.Add(summaryAgeCmd);
        root.Add(summaryRangeCmd);
        root.Add(exportSummaryMdCmd);
        root.Add(importSummaryMdCmd);
    }
}
