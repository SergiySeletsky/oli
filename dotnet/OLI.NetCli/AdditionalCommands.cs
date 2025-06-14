using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

public static class AdditionalCommands
{
    public static void Register(RootCommand root)
    {
        // list-commands
        var listCmd = new Command("list-commands", "List all available commands");
        listCmd.SetHandler(async () =>
        {
            foreach (var cmd in root.Children.OfType<Command>())
            {
                Console.WriteLine(cmd.Name);
            }
            await Task.CompletedTask;
        });

        // file-writable
        var filePathArg = new Argument<string>("path");
        var fileWritable = new Command("file-writable", "Check if file is writable") { filePathArg };
        fileWritable.SetHandler(async (string path) =>
        {
            bool writable = File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly) == false;
            Console.WriteLine(writable ? "writable" : "not writable");
            await Task.CompletedTask;
        }, filePathArg);

        // dir-writable
        var dirPathArg = new Argument<string>("path");
        var dirWritable = new Command("dir-writable", "Check if directory is writable") { dirPathArg };
        dirWritable.SetHandler(async (string path) =>
        {
            try
            {
                var test = Path.Combine(path, ".write_test");
                await File.WriteAllTextAsync(test, "test");
                File.Delete(test);
                Console.WriteLine("writable");
            }
            catch
            {
                Console.WriteLine("not writable");
            }
        }, dirPathArg);

        // directory-size
        var dirSize = new Command("directory-size", "Get directory size in bytes") { dirPathArg };
        dirSize.SetHandler(async (string path) =>
        {
            long size = 0;
            if (Directory.Exists(path))
            {
                size = Directory.GetFiles(path, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
            }
            Console.WriteLine(size);
            await Task.CompletedTask;
        }, dirPathArg);

        // memory-stats
        var memoryStats = new Command("memory-stats", "Show memory file statistics");
        memoryStats.SetHandler(async () =>
        {
            var lines = File.Exists(Program.MemoryPath) ? File.ReadAllLines(Program.MemoryPath) : Array.Empty<string>();
            var wordCount = lines.SelectMany(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Count();
            var uniqueWords = lines.SelectMany(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Select(w => w.ToLowerInvariant()).Distinct().Count();
            var size = File.Exists(Program.MemoryPath) ? new FileInfo(Program.MemoryPath).Length : 0;
            Console.WriteLine($"Lines: {lines.Length}, Words: {wordCount}, Unique: {uniqueWords}, Bytes: {size}");
            await Task.CompletedTask;
        });

        // memory-unique-words
        var memoryUnique = new Command("memory-unique-words", "Count unique words in memory file");
        memoryUnique.SetHandler(async () =>
        {
            var words = File.Exists(Program.MemoryPath)
                ? File.ReadAllLines(Program.MemoryPath)
                    .SelectMany(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    .Select(w => w.ToLowerInvariant())
                : Array.Empty<string>();
            Console.WriteLine(words.Distinct().Count());
            await Task.CompletedTask;
        });

        // task-rename
        var idArg = new Argument<string>("id");
        var descArg = new Argument<string>("description");
        var taskRename = new Command("task-rename", "Rename a task") { idArg, descArg };
        taskRename.SetHandler(async (string id, string desc) =>
        {
            var state = Program.LoadState();
            var task = state.Tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                task.Description = desc;
                task.UpdatedAt = DateTime.UtcNow;
                Program.SaveState(state);
                Console.WriteLine("renamed");
            }
            else
            {
                Console.WriteLine("task not found");
            }
            await Task.CompletedTask;
        }, idArg, descArg);

        // set-task-priority
        var priorityArg = new Argument<int>("priority");
        var setPriority = new Command("set-task-priority", "Set task priority") { idArg, priorityArg };
        setPriority.SetHandler(async (string id, int priority) =>
        {
            var state = Program.LoadState();
            var task = state.Tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                task.Priority = priority;
                task.UpdatedAt = DateTime.UtcNow;
                Program.SaveState(state);
                Console.WriteLine("priority set");
            }
            else
            {
                Console.WriteLine("task not found");
            }
            await Task.CompletedTask;
        }, idArg, priorityArg);

        // reopen-task
        var reopenTask = new Command("reopen-task", "Reopen a completed task") { idArg };
        reopenTask.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var task = state.Tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                task.Status = "in-progress";
                task.UpdatedAt = DateTime.UtcNow;
                Program.SaveState(state);
                Console.WriteLine("reopened");
            }
            else
            {
                Console.WriteLine("task not found");
            }
            await Task.CompletedTask;
        }, idArg);

        // conversation-to-html
        var htmlArg = new Argument<string>("path");
        var convToHtml = new Command("conversation-to-html", "Export conversation as HTML") { htmlArg };
        convToHtml.SetHandler(async (string path) =>
        {
            var state = Program.LoadState();
            var sb = new StringBuilder();
            sb.AppendLine("<html><body>");
            foreach (var line in state.Conversation)
            {
                sb.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(line)}</p>");
            }
            sb.AppendLine("</body></html>");
            await File.WriteAllTextAsync(path, sb.ToString());
        }, htmlArg);

        // rpc-events
        var rpcEvents = new Command("rpc-events", "Get pending RPC events");
        rpcEvents.SetHandler(async () =>
        {
            using var client = new HttpClient();
            try
            {
                var json = await client.GetStringAsync("http://localhost:5050/events");
                Console.WriteLine(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        });

        // set-log-level
        var levelArg = new Argument<string>("level");
        var setLog = new Command("set-log-level", "Set log verbosity") { levelArg };
        setLog.SetHandler(async (string level) =>
        {
            var state = Program.LoadState();
            state.LogLevel = level;
            Program.SaveState(state);
            Console.WriteLine($"level set to {level}");
            await Task.CompletedTask;
        }, levelArg);

        // show-log
        var linesOpt = new Option<int>("--lines", () => 20, "Number of lines to show");
        var showLog = new Command("show-log", "Display log file") { linesOpt };
        showLog.SetHandler(async (int lines) =>
        {
            Console.WriteLine(Program.ReadLog(lines));
            await Task.CompletedTask;
        }, linesOpt);

        // clear-log
        var clearLog = new Command("clear-log", "Clear log file");
        clearLog.SetHandler(async () =>
        {
            Program.ClearLog();
            Console.WriteLine("log cleared");
            await Task.CompletedTask;
        });

        // search-tasks
        var queryArg = new Argument<string>("query");
        var searchTasks = new Command("search-tasks", "Search tasks by description") { queryArg };
        searchTasks.SetHandler(async (string query) =>
        {
            var state = Program.LoadState();
            var matches = state.Tasks.Where(t => t.Description.Contains(query, StringComparison.OrdinalIgnoreCase));
            foreach (var t in matches) Console.WriteLine($"{t.Id} {t.Description} ({t.Status})");
            await Task.CompletedTask;
        }, queryArg);

        // task-history
        var taskHistory = new Command("task-history", "List tasks chronologically");
        taskHistory.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var t in state.Tasks.OrderBy(t => t.CreatedAt))
                Console.WriteLine($"{t.CreatedAt:u} {t.Id} {t.Status} {t.Description}");
            await Task.CompletedTask;
        });

        // dedupe-conversation
        var dedupeConv = new Command("dedupe-conversation", "Remove consecutive duplicate messages");
        dedupeConv.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var deduped = new List<string>();
            string? last = null;
            foreach (var line in state.Conversation)
            {
                if (line != last) deduped.Add(line);
                last = line;
            }
            state.Conversation = deduped;
            Program.SaveState(state);
            Console.WriteLine("deduplicated");
            await Task.CompletedTask;
        });

        // export-memory-section
        var sectionArg = new Argument<string>("section");
        var outArg = new Argument<string>("path");
        var exportSection = new Command("export-memory-section", "Export a memory section") { sectionArg, outArg };
        exportSection.SetHandler(async (string section, string path) =>
        {
            if (!File.Exists(Program.MemoryPath)) return;
            var lines = File.ReadAllLines(Program.MemoryPath);
            var sb = new StringBuilder();
            bool capture = false;
            foreach (var line in lines)
            {
                if (line.StartsWith("## "))
                {
                    capture = line.TrimStart('#', ' ').StartsWith(section);
                    continue;
                }
                if (capture) sb.AppendLine(line);
            }
            await File.WriteAllTextAsync(path, sb.ToString());
        }, sectionArg, outArg);

        // import-memory-section
        var importSection = new Command("import-memory-section", "Append section from file") { sectionArg, outArg };
        importSection.SetHandler(async (string section, string path) =>
        {
            var content = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            var sb = new StringBuilder();
            sb.AppendLine($"## {section}");
            sb.AppendLine(content);
            File.AppendAllText(Program.MemoryPath, sb.ToString());
            Console.WriteLine("imported");
            await Task.CompletedTask;
        }, sectionArg, outArg);

        // open-memory
        var openMemory = new Command("open-memory", "Open memory file in editor");
        openMemory.SetHandler(async () =>
        {
            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "xdg-open";
            process.StartInfo.ArgumentList.Add(Program.MemoryPath);
            process.Start();
            await Task.CompletedTask;
        });

        // list-memory-keys
        var listKeys = new Command("list-memory-keys", "List memory section headings");
        listKeys.SetHandler(async () =>
        {
            if (!File.Exists(Program.MemoryPath)) return;
            foreach (var line in File.ReadLines(Program.MemoryPath))
                if (line.StartsWith("## ")) Console.WriteLine(line.TrimStart('#', ' '));
            await Task.CompletedTask;
        });

        root.Add(listCmd);
        root.Add(fileWritable);
        root.Add(dirWritable);
        root.Add(dirSize);
        root.Add(memoryStats);
        root.Add(memoryUnique);
        root.Add(taskRename);
        root.Add(setPriority);
        root.Add(reopenTask);
        root.Add(convToHtml);
        root.Add(rpcEvents);
        root.Add(setLog);
        root.Add(showLog);
        root.Add(clearLog);
        root.Add(searchTasks);
        root.Add(taskHistory);
        root.Add(dedupeConv);
        root.Add(exportSection);
        root.Add(importSection);
        root.Add(openMemory);
        root.Add(listKeys);
    }
}
