using System;
using System.CommandLine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;

public static class ConversationCommands
{
    public static void Register(RootCommand root)
    {
        // clear-conversation
        var clearConv = new Command("clear-conversation", "Clear stored conversation");
        clearConv.SetHandler(() =>
        {
            var state = Program.LoadState();
            state.Conversation.Clear();
            Program.SaveState(state);
            Console.WriteLine("Conversation cleared");
            return Task.CompletedTask;
        });

        // conversation
        var conversation = new Command("conversation", "Show conversation history");
        conversation.SetHandler(() =>
        {
            var state = Program.LoadState();
            if (state.Conversation.Count == 0)
            {
                Console.WriteLine("No conversation yet.");
            }
            foreach (var m in state.Conversation)
            {
                Console.WriteLine(m);
            }
            return Task.CompletedTask;
        });

        // save-conversation
        var savePathOpt = new Option<string>("--path") { IsRequired = true };
        var saveConv = new Command("save-conversation", "Save conversation to file") { savePathOpt };
        saveConv.SetHandler((string path) =>
        {
            var state = Program.LoadState();
            File.WriteAllLines(path, state.Conversation);
            Console.WriteLine($"Conversation saved to {path}");
            return Task.CompletedTask;
        }, savePathOpt);

        // export-conversation
        var exportPathOpt = new Option<string>("--path") { IsRequired = true };
        var exportConv = new Command("export-conversation", "Export conversation to file") { exportPathOpt };
        exportConv.SetHandler((string path) =>
        {
            var state = Program.LoadState();
            File.WriteAllLines(path, state.Conversation);
            Console.WriteLine($"Conversation exported to {path}");
            return Task.CompletedTask;
        }, exportPathOpt);

        // import-conversation
        var importPathOpt = new Option<string>("--path") { IsRequired = true };
        var importConv = new Command("import-conversation", "Load conversation from file") { importPathOpt };
        importConv.SetHandler(async (string path) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return;
            }
            var lines = File.ReadAllLines(path).ToList();
            var state = Program.LoadState();
            state.Conversation = lines;
            await Program.AutoCompress(state);
            Program.SaveState(state);
            Console.WriteLine("Conversation loaded");
        }, importPathOpt);

        // append-conversation
        var appendPathOpt = new Option<string>("--path") { IsRequired = true };
        var appendConv = new Command("append-conversation", "Append messages from file to conversation") { appendPathOpt };
        appendConv.SetHandler(async (string path) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return;
            }
            var lines = File.ReadAllLines(path);
            var state = Program.LoadState();
            state.Conversation.AddRange(lines);
            await Program.AutoCompress(state);
            Program.SaveState(state);
            Console.WriteLine("Conversation updated");
        }, appendPathOpt);

        // delete-conversation-message
        var deleteIndexOpt = new Option<int>("--index") { IsRequired = true };
        var deleteMessage = new Command("delete-conversation-message", "Remove a message by index") { deleteIndexOpt };
        deleteMessage.SetHandler((int index) =>
        {
            var state = Program.LoadState();
            if (index >= 0 && index < state.Conversation.Count)
            {
                state.Conversation.RemoveAt(index);
                Program.SaveState(state);
                Console.WriteLine("Message removed");
            }
            else
            {
                Console.WriteLine("Invalid index");
            }
            return Task.CompletedTask;
        }, deleteIndexOpt);

        // summarize-conversation
        var summarizeConv = new Command("summarize-conversation", "Summarize stored conversation");
        summarizeConv.SetHandler(async () =>
        {
            var state = Program.LoadState();
            if (state.Conversation.Count == 0)
            {
                Console.WriteLine("No conversation to summarize");
                return;
            }
            var text = string.Join("\n", state.Conversation);
            string summary;
            try
            {
                summary = await KernelUtils.SummarizeAsync(text);
            }
            catch
            {
                summary = Program.GenerateSummary(text);
            }
            state.Conversation.Clear();
            state.Conversation.Add($"[SUMMARY] {summary}");
            Program.SaveState(state);
            Console.WriteLine(summary);
        });

        // conversation-stats
        var convStats = new Command("conversation-stats", "Show conversation statistics");
        convStats.SetHandler(() =>
        {
            var state = Program.LoadState();
            Console.WriteLine($"Messages:{state.Conversation.Count}");
            return Task.CompletedTask;
        });

        // conversation-char-count
        var convChar = new Command("conversation-char-count", "Show total character count of conversation");
        convChar.SetHandler(() =>
        {
            var state = Program.LoadState();
            var count = state.Conversation.Sum(m => m.Length);
            Console.WriteLine(count);
            return Task.CompletedTask;
        });

        // conversation-word-count
        var convWords = new Command("conversation-word-count", "Show total word count of conversation");
        convWords.SetHandler(() =>
        {
            var state = Program.LoadState();
            var count = state.Conversation.Sum(m => m.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
            Console.WriteLine(count);
            return Task.CompletedTask;
        });

        // compress-conversation
        var compressConv = new Command("compress-conversation", "Summarize and clear conversation");
        compressConv.SetHandler(async () =>
        {
            var state = Program.LoadState();
            if (state.Conversation.Count == 0)
            {
                Console.WriteLine("No conversation to compress");
                return;
            }
            var text = string.Join("\n", state.Conversation);
            string summary;
            try
            {
                summary = await KernelUtils.SummarizeAsync(text);
            }
            catch
            {
                summary = Program.GenerateSummary(text);
            }
            state.ConversationSummaries.Add(new ConversationSummary
            {
                Content = summary,
                CreatedAt = DateTime.UtcNow,
                MessagesCount = state.Conversation.Count,
                OriginalChars = text.Length
            });
            state.Conversation.Clear();
            Program.SaveState(state);
            Console.WriteLine(summary);
        });

        // clear-history
        var clearHistory = new Command("clear-history", "Remove conversation and summaries");
        clearHistory.SetHandler(() =>
        {
            var state = Program.LoadState();
            state.Conversation.Clear();
            state.ConversationSummaries.Clear();
            Program.SaveState(state);
            Console.WriteLine("History cleared");
            return Task.CompletedTask;
        });

        root.Add(clearConv);
        root.Add(conversation);
        root.Add(saveConv);
        root.Add(exportConv);
        root.Add(importConv);
        root.Add(appendConv);
        root.Add(deleteMessage);
        root.Add(summarizeConv);
        root.Add(convStats);
        root.Add(convChar);
        root.Add(convWords);
        root.Add(compressConv);
        root.Add(clearHistory);

        // conversation-exists
        var conversationExistsCmd = new Command("conversation-exists", "Check if conversation has messages");
        conversationExistsCmd.SetHandler(() =>
        {
            var state = Program.LoadState();
            Console.WriteLine(state.Conversation.Count > 0 ? "true" : "false");
            return Task.CompletedTask;
        });

        // conversation-has
        var hasTextArg = new Argument<string>("text");
        var conversationHasCmd = new Command("conversation-has", "Check if conversation contains text") { hasTextArg };
        conversationHasCmd.SetHandler((string text) =>
        {
            var state = Program.LoadState();
            bool found = state.Conversation.Any(m => m.Contains(text, StringComparison.OrdinalIgnoreCase));
            Console.WriteLine(found ? "true" : "false");
            return Task.CompletedTask;
        }, hasTextArg);

        // remove-empty-conversation
        var removeEmptyConvCmd = new Command("remove-empty-conversation", "Delete empty lines from conversation");
        removeEmptyConvCmd.SetHandler(() =>
        {
            var state = Program.LoadState();
            int before = state.Conversation.Count;
            state.Conversation = state.Conversation.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            Program.SaveState(state);
            Console.WriteLine(before - state.Conversation.Count);
            return Task.CompletedTask;
        });

        // conversation-last-n
        var lastNArg = new Argument<int>("count");
        var conversationLastNCmd = new Command("conversation-last-n", "Show last N messages") { lastNArg };
        conversationLastNCmd.SetHandler((int count) =>
        {
            var state = Program.LoadState();
            foreach (var line in state.Conversation.TakeLast(count)) Console.WriteLine(line);
            return Task.CompletedTask;
        }, lastNArg);

        // conversation-to-csv
        var convCsvArg = new Argument<string>("path");
        var conversationToCsvCmd = new Command("conversation-to-csv", "Export conversation to CSV") { convCsvArg };
        conversationToCsvCmd.SetHandler(async (string path) =>
        {
            var state = Program.LoadState();
            using var writer = new StreamWriter(path);
            await writer.WriteLineAsync("role,content");
            foreach (var line in state.Conversation)
            {
                string role = line.StartsWith("[user]") || line.StartsWith("User:") ? "user" :
                              line.StartsWith("[assistant]") || line.StartsWith("Assistant:") ? "assistant" :
                              line.StartsWith("[system]") || line.StartsWith("System:") ? "system" : "unknown";
                string content = line.Contains(']') ? line.Split(']', 2)[1].Trim() : line;
                await writer.WriteLineAsync($"{EscapeCsv(role)},{EscapeCsv(content)}");
            }
        }, convCsvArg);

        // conversation-from-csv
        var fromCsvArg = new Argument<string>("path");
        var conversationFromCsvCmd = new Command("conversation-from-csv", "Import conversation from CSV") { fromCsvArg };
        conversationFromCsvCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("file not found"); return; }
            var lines = File.ReadAllLines(path).Skip(1);
            var list = new List<string>();
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length < 2) continue;
                var role = parts[0].Trim();
                var content = string.Join(',', parts.Skip(1)).Trim();
                string prefix = role switch
                {
                    "user" => "User: ",
                    "assistant" => "Assistant: ",
                    "system" => "System: ",
                    _ => string.Empty
                };
                list.Add($"{prefix}{content}");
            }
            var state = Program.LoadState();
            state.Conversation = list;
            Program.SaveState(state);
            Console.WriteLine("imported");
            await Task.CompletedTask;
        }, fromCsvArg);

        // conversation-average-length
        var convAvgCmd = new Command("conversation-average-length", "Average message length");
        convAvgCmd.SetHandler(() =>
        {
            var state = Program.LoadState();
            if (state.Conversation.Count == 0) Console.WriteLine("0");
            else
            {
                var avg = state.Conversation.Average(m => m.Length);
                Console.WriteLine(avg.ToString("F2"));
            }
            return Task.CompletedTask;
        });

        // conversation-first-n
        var firstNArg = new Argument<int>("count");
        var conversationFirstNCmd = new Command("conversation-first-n", "Show first N messages") { firstNArg };
        conversationFirstNCmd.SetHandler((int count) =>
        {
            var state = Program.LoadState();
            foreach (var line in state.Conversation.Take(count)) Console.WriteLine(line);
            return Task.CompletedTask;
        }, firstNArg);

        // conversation-shuffle
        var conversationShuffleCmd = new Command("conversation-shuffle", "Randomize conversation order");
        conversationShuffleCmd.SetHandler(() =>
        {
            var state = Program.LoadState();
            var rnd = new Random();
            state.Conversation = state.Conversation.OrderBy(_ => rnd.Next()).ToList();
            Program.SaveState(state);
            Console.WriteLine("shuffled");
            return Task.CompletedTask;
        });

        // conversation-to-json
        var convJsonArg = new Argument<string>("path");
        var conversationToJsonCmd = new Command("conversation-to-json", "Export conversation as JSON array") { convJsonArg };
        conversationToJsonCmd.SetHandler(async (string path) =>
        {
            var state = Program.LoadState();
            var json = JsonSerializer.Serialize(state.Conversation, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(path, json);
        }, convJsonArg);

        // conversation-from-json
        var fromJsonArg = new Argument<string>("path");
        var conversationFromJsonCmd = new Command("conversation-from-json", "Load conversation from JSON array") { fromJsonArg };
        conversationFromJsonCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("file not found"); return; }
            var json = await File.ReadAllTextAsync(path);
            var list = JsonSerializer.Deserialize<List<string>>(json);
            if (list != null)
            {
                var state = Program.LoadState();
                state.Conversation = list;
                Program.SaveState(state);
                Console.WriteLine("loaded");
            }
            else Console.WriteLine("invalid file");
        }, fromJsonArg);

        root.Add(conversationExistsCmd);
        root.Add(conversationHasCmd);
        root.Add(removeEmptyConvCmd);
        root.Add(conversationLastNCmd);
        root.Add(conversationToCsvCmd);
        root.Add(conversationFromCsvCmd);
        root.Add(convAvgCmd);
        root.Add(conversationFirstNCmd);
        root.Add(conversationShuffleCmd);
        root.Add(conversationToJsonCmd);
        root.Add(conversationFromJsonCmd);
    }

    static string EscapeCsv(string s)
    {
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
