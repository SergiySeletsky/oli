using System;
using System.CommandLine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
        importConv.SetHandler((string path) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return Task.CompletedTask;
            }
            var lines = File.ReadAllLines(path).ToList();
            var state = Program.LoadState();
            state.Conversation = lines;
            Program.AutoCompress(state);
            Program.SaveState(state);
            Console.WriteLine("Conversation loaded");
            return Task.CompletedTask;
        }, importPathOpt);

        // append-conversation
        var appendPathOpt = new Option<string>("--path") { IsRequired = true };
        var appendConv = new Command("append-conversation", "Append messages from file to conversation") { appendPathOpt };
        appendConv.SetHandler((string path) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return Task.CompletedTask;
            }
            var lines = File.ReadAllLines(path);
            var state = Program.LoadState();
            state.Conversation.AddRange(lines);
            Program.AutoCompress(state);
            Program.SaveState(state);
            Console.WriteLine("Conversation updated");
            return Task.CompletedTask;
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
                summary = await Program.SummarizeAsync(text);
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
                summary = await Program.SummarizeAsync(text);
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
    }
}
