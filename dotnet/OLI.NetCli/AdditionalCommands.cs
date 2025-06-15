using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using static KernelUtils;
using System.Collections.Generic;
using System.Threading.Tasks;
using static LogUtils;
using static BackupUtils;

public static class AdditionalCommands
{
    static readonly HashSet<string> StopWords = new(
        new[] { "the","and","a","to","of","in","is","it","that","on","for","with","as","at","by","an","be","this","from" });
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

        // show-config
        var showConfigCmd = new Command("show-config", "Display configuration settings");
        showConfigCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            Console.WriteLine($"Model:{state.SelectedModel} AgentMode:{state.AgentMode} AutoCompress:{state.AutoCompress} CharThresh:{state.CompressCharThreshold} MsgThresh:{state.CompressMessageThreshold} WorkingDir:{state.WorkingDirectory}");
            await Task.CompletedTask;
        });

        // estimate-tokens
        var estTextArg = new Argument<string>("text");
        var estimateTokensCmd = new Command("estimate-tokens", "Rough token estimate") { estTextArg };
        estimateTokensCmd.SetHandler(async (string text) =>
        {
            Console.WriteLine(Program.EstimateTokens(text));
            await Task.CompletedTask;
        }, estTextArg);

        // extract-metadata
        var metaMsgArg = new Argument<string>("message");
        var extractMetaCmd = new Command("extract-metadata", "Parse file path and line count") { metaMsgArg };
        extractMetaCmd.SetHandler(async (string message) =>
        {
            var (file, lines) = Program.ExtractToolMetadata(message);
            Console.WriteLine(JsonSerializer.Serialize(new { file, lines }));
            await Task.CompletedTask;
        }, metaMsgArg);

        // tool-description
        var toolNameArg = new Argument<string>("name");
        var fileArg = new Option<string?>("--file");
        var linesArg = new Option<int?>("--lines");
        var toolDescCmd = new Command("tool-description", "Get description for a tool") { toolNameArg, fileArg, linesArg };
        toolDescCmd.SetHandler(async (string name, string? file, int? lines) =>
        {
            Console.WriteLine(Program.ToolDescription(name, file, lines));
            await Task.CompletedTask;
        }, toolNameArg, fileArg, linesArg);

        // has-active-tasks
        var hasActiveCmd = new Command("has-active-tasks", "Any tasks in progress?");
        hasActiveCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            Console.WriteLine(state.Tasks.Any(t => t.Status == "in-progress") ? "true" : "false");
            await Task.CompletedTask;
        });

        // task-statuses
        var taskStatusesCmd = new Command("task-statuses", "JSON of task statuses");
        taskStatusesCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var statuses = state.Tasks.Select(t => new { t.Id, t.Description, t.Status, t.ToolCount, t.InputTokens, t.OutputTokens, CreatedAt = t.CreatedAt, UpdatedAt = t.UpdatedAt });
            Console.WriteLine(JsonSerializer.Serialize(statuses, new JsonSerializerOptions { WriteIndented = true }));
            await Task.CompletedTask;
        });

        // validate-api-key
        var keyModelArg = new Argument<string>("model");
        var apiKeyArg = new Argument<string>("key");
        var validateKeyCmd = new Command("validate-api-key", "Check API key for model") { keyModelArg, apiKeyArg };
        validateKeyCmd.SetHandler(async (string model, string key) =>
        {
            Console.WriteLine(Program.ValidateApiKey(model, key) ? "valid" : "invalid");
            await Task.CompletedTask;
        }, keyModelArg, apiKeyArg);

        // determine-provider
        var modelFileArg = new Argument<string>("file");
        var determineProviderCmd = new Command("determine-provider", "Show provider and agent model") { keyModelArg, apiKeyArg, modelFileArg };
        determineProviderCmd.SetHandler(async (string model, string key, string file) =>
        {
            var (prov, agent) = Program.DetermineProvider(model, key, file);
            Console.WriteLine($"{prov}:{agent}");
            await Task.CompletedTask;
        }, keyModelArg, apiKeyArg, modelFileArg);

        // display-to-session
        var displayPathArg = new Argument<string>("path");
        var displayToSessionCmd = new Command("display-to-session", "Convert display messages to session format") { displayPathArg };
        displayToSessionCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }
            var lines = File.ReadAllLines(path);
            var session = Program.DisplayToSession(lines);
            Console.WriteLine(JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true }));
            await Task.CompletedTask;
        }, displayPathArg);

        // session-to-display
        var sessionPathArg = new Argument<string>("path");
        var sessionToDisplayCmd = new Command("session-to-display", "Convert session messages to display format") { sessionPathArg };
        sessionToDisplayCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }
            var lines = File.ReadAllLines(path);
            var display = Program.SessionToDisplay(lines);
            Console.WriteLine(string.Join("\n", display));
            await Task.CompletedTask;
        }, sessionPathArg);

        // summarize-text
        var sumTextArg = new Argument<string>("text");
        var summarizeTextCmd = new Command("summarize-text", "Summarize provided text") { sumTextArg };
        summarizeTextCmd.SetHandler(async (string text) =>
        {
            try
            {
                var summary = await KernelUtils.SummarizeAsync(text);
                Console.WriteLine(summary);
            }
            catch
            {
                Console.WriteLine(Program.GenerateSummary(text));
            }
        }, sumTextArg);

        // summarize-file
        var sumFileArg = new Argument<string>("path");
        var summarizeFileCmd = new Command("summarize-file", "Summarize a file") { sumFileArg };
        summarizeFileCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("file not found"); return; }
            var text = File.ReadAllText(path);
            string summary;
            try { summary = await KernelUtils.SummarizeAsync(text); }
            catch { summary = Program.GenerateSummary(text); }
            Console.WriteLine(summary);
        }, sumFileArg);

        // summarize-memory-section
        var sumSectionArg = new Argument<string>("section");
        var summarizeMemorySectionCmd = new Command("summarize-memory-section", "Summarize a memory section") { sumSectionArg };
        summarizeMemorySectionCmd.SetHandler(async (string section) =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("No memory file"); return; }
            var lines = File.ReadAllLines(Program.MemoryPath);
            var sb = new StringBuilder();
            bool capture = false;
            foreach (var line in lines)
            {
                if (line.StartsWith("## "))
                {
                    if (capture) break;
                    capture = line[3..].Trim() == section;
                    continue;
                }
                if (capture) sb.AppendLine(line);
            }
            var text = sb.ToString();
            string summary;
            try { summary = await KernelUtils.SummarizeAsync(text); }
            catch { summary = Program.GenerateSummary(text); }
            Console.WriteLine(summary);
        }, sumSectionArg);

        // summarize-tasks
        var summarizeTasksCmd = new Command("summarize-tasks", "Summarize all tasks");
        summarizeTasksCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var text = string.Join("\n", state.Tasks.Select(t => $"{t.Id}: {t.Description} [{t.Status}]"));
            string summary;
            try { summary = await KernelUtils.SummarizeAsync(text); }
            catch { summary = Program.GenerateSummary(text); }
            Console.WriteLine(summary);
        });

        // summarize-state
        var summarizeStateCmd = new Command("summarize-state", "Summarize entire state file");
        summarizeStateCmd.SetHandler(async () =>
        {
            var json = File.Exists(Program.StatePath) ? File.ReadAllText(Program.StatePath) : "{}";
            string summary;
            try { summary = await KernelUtils.SummarizeAsync(json); }
            catch { summary = Program.GenerateSummary(json); }
            Console.WriteLine(summary);
        });

        // conversation-word-frequency
        var convTopOpt = new Option<int>("--top", () => 10);
        var conversationWordFreqCmd = new Command("conversation-word-frequency", "Top words in conversation") { convTopOpt };
        conversationWordFreqCmd.SetHandler(async (int top) =>
        {
            var state = Program.LoadState();
            var words = state.Conversation
                .SelectMany(l => l.Split(new[]{' ','\n','\r','\t'}, StringSplitOptions.RemoveEmptyEntries))
                .Select(w => w.ToLowerInvariant());
            var freq = words.GroupBy(w => w).Select(g => (Word:g.Key, Count:g.Count()))
                .OrderByDescending(g => g.Count).Take(top);
            foreach (var (word,count) in freq) Console.WriteLine($"{word}:{count}");
            await Task.CompletedTask;
        }, convTopOpt);

        // conversation-unique-words
        var conversationUniqueCmd = new Command("conversation-unique-words", "Count unique words in conversation");
        conversationUniqueCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var unique = state.Conversation
                .SelectMany(l => l.Split(new[]{' ','\n','\r','\t'}, StringSplitOptions.RemoveEmptyEntries))
                .Select(w => w.ToLowerInvariant())
                .Distinct()
                .Count();
            Console.WriteLine(unique);
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

        // set-task-due
        var dueOpt = new Option<string>("--due") { IsRequired = true };
        var setDueCmd = new Command("set-task-due", "Set due date for a task") { idArg, dueOpt };
        setDueCmd.SetHandler(async (string id, string due) =>
        {
            if (!DateTime.TryParse(due, out var dt)) { Console.WriteLine("invalid date"); return; }
            var state = Program.LoadState();
            var task = state.Tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                task.DueDate = dt;
                task.UpdatedAt = DateTime.UtcNow;
                Program.SaveState(state);
                Console.WriteLine("due date set");
            }
            else Console.WriteLine("task not found");
            await Task.CompletedTask;
        }, idArg, dueOpt);

        // task-due
        var taskDueCmd = new Command("task-due", "Show task due date") { idArg };
        taskDueCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var task = state.Tasks.FirstOrDefault(t => t.Id == id);
            if (task?.DueDate != null) Console.WriteLine(task.DueDate.Value.ToString("u"));
            else Console.WriteLine("no due date");
            await Task.CompletedTask;
        }, idArg);

        // add-task-tag
        var tagArg = new Argument<string>("tag");
        var addTagCmd = new Command("add-task-tag", "Add tag to task") { idArg, tagArg };
        addTagCmd.SetHandler(async (string id, string tag) =>
        {
            var state = Program.LoadState();
            var task = state.Tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                if (!task.Tags.Contains(tag)) task.Tags.Add(tag);
                Program.SaveState(state);
                Console.WriteLine("tag added");
            }
            else Console.WriteLine("task not found");
            await Task.CompletedTask;
        }, idArg, tagArg);

        // remove-task-tag
        var removeTagCmd = new Command("remove-task-tag", "Remove tag from task") { idArg, tagArg };
        removeTagCmd.SetHandler(async (string id, string tag) =>
        {
            var state = Program.LoadState();
            var task = state.Tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                task.Tags.Remove(tag);
                Program.SaveState(state);
                Console.WriteLine("tag removed");
            }
            else Console.WriteLine("task not found");
            await Task.CompletedTask;
        }, idArg, tagArg);

        // list-task-tags
        var listTagsCmd = new Command("list-task-tags", "List tags for a task") { idArg };
        listTagsCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var task = state.Tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                foreach (var tag in task.Tags) Console.WriteLine(tag);
            }
            else Console.WriteLine("task not found");
            await Task.CompletedTask;
        }, idArg);

        // task-exists
        var taskExistsCmd = new Command("task-exists", "Check if task id exists") { idArg };
        taskExistsCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            Console.WriteLine(state.Tasks.Any(t => t.Id == id) ? "true" : "false");
            await Task.CompletedTask;
        }, idArg);

        // count-task-tags
        var countTagsCmd = new Command("count-task-tags", "Show tag usage counts");
        countTagsCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var counts = state.Tasks.SelectMany(t => t.Tags)
                .GroupBy(t => t)
                .Select(g => (Tag: g.Key, Count: g.Count()))
                .OrderByDescending(g => g.Count);
            foreach (var (tag, c) in counts) Console.WriteLine($"{tag}:{c}");
            await Task.CompletedTask;
        });

        // tasks-by-tag
        var tasksByTagCmd = new Command("tasks-by-tag", "List tasks with given tag") { tagArg };
        tasksByTagCmd.SetHandler(async (string tag) =>
        {
            var state = Program.LoadState();
            foreach (var t in state.Tasks.Where(t => t.Tags.Contains(tag)))
                Console.WriteLine($"{t.Id}: {t.Description}");
            await Task.CompletedTask;
        }, tagArg);

        var tasksTagSearchCmd = new Command("search-task-tags", "Search tasks by tag substring") { tagArg };
        tasksTagSearchCmd.SetHandler(async (string tag) =>
        {
            var state = Program.LoadState();
            foreach (var t in state.Tasks.Where(t => t.Tags.Any(x => x.Contains(tag, StringComparison.OrdinalIgnoreCase))))
                Console.WriteLine($"{t.Id}: {t.Description}");
            await Task.CompletedTask;
        }, tagArg);

        // tasks-due-soon
        var daysOpt = new Option<int>("--days", () => 7);
        var dueSoonCmd = new Command("tasks-due-soon", "List tasks due within days") { daysOpt };
        dueSoonCmd.SetHandler(async (int days) =>
        {
            var state = Program.LoadState();
            var limit = DateTime.UtcNow.AddDays(days);
            foreach (var t in state.Tasks.Where(t => t.DueDate != null && t.DueDate <= limit && t.DueDate >= DateTime.UtcNow))
                Console.WriteLine($"{t.Id}: {t.Description} due {t.DueDate:u}");
            await Task.CompletedTask;
        }, daysOpt);

        // tasks-due-range
        var startOpt = new Option<string>("--start") { IsRequired = true };
        var endOpt = new Option<string>("--end") { IsRequired = true };
        var tasksRangeCmd = new Command("tasks-due-range", "List tasks due between two dates") { startOpt, endOpt };
        tasksRangeCmd.SetHandler(async (string start, string end) =>
        {
            if (!DateTime.TryParse(start, out var s) || !DateTime.TryParse(end, out var e))
            {
                Console.WriteLine("invalid dates");
                return;
            }
            var state = Program.LoadState();
            foreach (var t in state.Tasks.Where(t => t.DueDate != null && t.DueDate >= s && t.DueDate <= e))
                Console.WriteLine($"{t.Id}: {t.Description} due {t.DueDate:u}");
            await Task.CompletedTask;
        }, startOpt, endOpt);

        // tasks-due-tomorrow
        var tasksTomorrowCmd = new Command("tasks-due-tomorrow", "List tasks due tomorrow");
        tasksTomorrowCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var tomorrow = DateTime.UtcNow.Date.AddDays(1);
            foreach (var t in state.Tasks.Where(t => t.DueDate?.Date == tomorrow))
                Console.WriteLine($"{t.Id} {t.Description}");
            await Task.CompletedTask;
        });

        // tasks-overdue
        var overdueCmd = new Command("tasks-overdue", "List overdue tasks");
        overdueCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var t in state.Tasks.Where(t => t.DueDate != null && t.DueDate < DateTime.UtcNow))
                Console.WriteLine($"{t.Id}: {t.Description} due {t.DueDate:u}");
            await Task.CompletedTask;
        });

        // tasks-due-today
        var tasksTodayCmd = new Command("tasks-due-today", "List tasks due today");
        tasksTodayCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var today = DateTime.UtcNow.Date;
            foreach (var t in state.Tasks.Where(t => t.DueDate?.Date == today))
                Console.WriteLine($"{t.Id} {t.Description}");
            await Task.CompletedTask;
        });

        // tasks-due-next-week
        var tasksNextWeekCmd = new Command("tasks-due-next-week", "List tasks due in the next 7 days");
        tasksNextWeekCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var start = DateTime.UtcNow.Date.AddDays(1);
            var end = DateTime.UtcNow.Date.AddDays(7);
            foreach (var t in state.Tasks.Where(t => t.DueDate != null && t.DueDate.Value.Date >= start && t.DueDate.Value.Date <= end))
                Console.WriteLine($"{t.Id}: {t.Description} due {t.DueDate:u}");
            await Task.CompletedTask;
        });

        // tasks-this-month
        var tasksThisMonthCmd = new Command("tasks-this-month", "List tasks created this month");
        tasksThisMonthCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var start = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            foreach (var t in state.Tasks.Where(t => t.CreatedAt >= start))
                Console.WriteLine($"{t.Id}: {t.Description}");
            await Task.CompletedTask;
        });

        // pause-task
        var pauseTaskCmd = new Command("pause-task", "Pause a task") { idArg };
        pauseTaskCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var task = state.Tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                task.Paused = true;
                Program.SaveState(state);
                Console.WriteLine("paused");
            }
            else Console.WriteLine("task not found");
            await Task.CompletedTask;
        }, idArg);

        // resume-task
        var resumeTaskCmd = new Command("resume-task", "Resume a paused task") { idArg };
        resumeTaskCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var task = state.Tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                task.Paused = false;
                Program.SaveState(state);
                Console.WriteLine("resumed");
            }
            else Console.WriteLine("task not found");
            await Task.CompletedTask;
        }, idArg);

        // pause-all-tasks
        var pauseAllCmd = new Command("pause-all-tasks", "Pause all tasks");
        pauseAllCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var t in state.Tasks) t.Paused = true;
            Program.SaveState(state);
            Console.WriteLine("paused all");
            await Task.CompletedTask;
        });

        // resume-all-tasks
        var resumeAllCmd = new Command("resume-all-tasks", "Resume all tasks");
        resumeAllCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var t in state.Tasks) t.Paused = false;
            Program.SaveState(state);
            Console.WriteLine("resumed all");
            await Task.CompletedTask;
        });

        // tasks-paused
        var tasksPausedCmd = new Command("tasks-paused", "List paused tasks");
        tasksPausedCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var t in state.Tasks.Where(t => t.Paused))
                Console.WriteLine($"{t.Id}: {t.Description}");
            await Task.CompletedTask;
        });

        // tasks-created-before
        var createdBeforeArg = new Argument<string>("date");
        var tasksCreatedBeforeCmd = new Command("tasks-created-before", "List tasks created before date") { createdBeforeArg };
        tasksCreatedBeforeCmd.SetHandler(async (string date) =>
        {
            if (!DateTime.TryParse(date, out var dt)) { Console.WriteLine("invalid date"); return; }
            var state = Program.LoadState();
            foreach (var t in state.Tasks.Where(t => t.CreatedAt < dt))
                Console.WriteLine($"{t.Id}: {t.Description}");
            await Task.CompletedTask;
        }, createdBeforeArg);

        // tasks-created-after
        var createdAfterArg = new Argument<string>("date");
        var tasksCreatedAfterCmd = new Command("tasks-created-after", "List tasks created after date") { createdAfterArg };
        tasksCreatedAfterCmd.SetHandler(async (string date) =>
        {
            if (!DateTime.TryParse(date, out var dt)) { Console.WriteLine("invalid date"); return; }
            var state = Program.LoadState();
            foreach (var t in state.Tasks.Where(t => t.CreatedAt > dt))
                Console.WriteLine($"{t.Id}: {t.Description}");
            await Task.CompletedTask;
        }, createdAfterArg);

        // archive-task
        var archiveTaskCmd = new Command("archive-task", "Archive a task") { idArg };
        archiveTaskCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var task = state.Tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                task.Archived = true;
                Program.SaveState(state);
                Console.WriteLine("archived");
            }
            else Console.WriteLine("task not found");
            await Task.CompletedTask;
        }, idArg);

        // unarchive-task
        var unarchiveTaskCmd = new Command("unarchive-task", "Unarchive a task") { idArg };
        unarchiveTaskCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var task = state.Tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                task.Archived = false;
                Program.SaveState(state);
                Console.WriteLine("unarchived");
            }
            else Console.WriteLine("task not found");
            await Task.CompletedTask;
        }, idArg);

        // archived-tasks
        var archivedTasksCmd = new Command("archived-tasks", "List archived tasks");
        archivedTasksCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var t in state.Tasks.Where(t => t.Archived))
                Console.WriteLine($"{t.Id}: {t.Description}");
            await Task.CompletedTask;
        });

        // list-task-ids
        var listTaskIdsCmd = new Command("list-task-ids", "List all task IDs");
        listTaskIdsCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var t in state.Tasks) Console.WriteLine(t.Id);
            await Task.CompletedTask;
        });



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
            Console.WriteLine(LogUtils.ReadLog(lines));
            await Task.CompletedTask;
        }, linesOpt);

        // clear-log
        var clearLog = new Command("clear-log", "Clear log file");
        clearLog.SetHandler(async () =>
        {
            LogUtils.ClearLog();
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

        // log-path
        var logPathCmd = new Command("log-path", "Show path to log file");
        logPathCmd.SetHandler(async () =>
        {
            Console.WriteLine(LogUtils.LogPath);
            await Task.CompletedTask;
        });

        // search-log
        var logQueryArg = new Argument<string>("query");
        var searchLogCmd = new Command("search-log", "Search log file for text") { logQueryArg };
        searchLogCmd.SetHandler(async (string query) =>
        {
            foreach (var line in LogUtils.SearchLog(query)) Console.WriteLine(line);
            await Task.CompletedTask;
        }, logQueryArg);

        // export-log
        var outLogArg = new Argument<string>("path");
        var exportLogCmd = new Command("export-log", "Export log file to path") { outLogArg };
        exportLogCmd.SetHandler(async (string path) =>
        {
            LogUtils.ExportLog(path);
            Console.WriteLine($"exported to {path}");
            await Task.CompletedTask;
        }, outLogArg);

        // backup-state
        var backupStateCmd = new Command("backup-state", "Backup state file");
        backupStateCmd.SetHandler(async () =>
        {
            var dest = BackupUtils.BackupFile(Program.StatePath, "state.json");
            Console.WriteLine(dest);
            await Task.CompletedTask;
        });

        // restore-state
        var restoreStateArg = new Argument<string>("path");
        var restoreStateCmd = new Command("restore-state", "Restore state from backup") { restoreStateArg };
        restoreStateCmd.SetHandler(async (string path) =>
        {
            BackupUtils.RestoreFile(path, Program.StatePath);
            Console.WriteLine("restored");
            await Task.CompletedTask;
        }, restoreStateArg);

        // backup-memory
        var backupMemoryCmd = new Command("backup-memory", "Backup memory file");
        backupMemoryCmd.SetHandler(async () =>
        {
            var dest = BackupUtils.BackupFile(Program.MemoryPath, "memory.md");
            Console.WriteLine(dest);
            await Task.CompletedTask;
        });

        // restore-memory
        var restoreMemoryArg = new Argument<string>("path");
        var restoreMemoryCmd = new Command("restore-memory", "Restore memory from backup") { restoreMemoryArg };
        restoreMemoryCmd.SetHandler(async (string path) =>
        {
            BackupUtils.RestoreFile(path, Program.MemoryPath);
            Console.WriteLine("restored");
            await Task.CompletedTask;
        }, restoreMemoryArg);

        // backup-conversation
        var backupConvCmd = new Command("backup-conversation", "Backup conversation file");
        backupConvCmd.SetHandler(async () =>
        {
            var dest = BackupUtils.BackupFile(Program.ConversationPath, "conversation.json");
            Console.WriteLine(dest);
            await Task.CompletedTask;
        });

        // restore-conversation
        var restoreConvArg = new Argument<string>("path");
        var restoreConvCmd = new Command("restore-conversation", "Restore conversation from backup") { restoreConvArg };
        restoreConvCmd.SetHandler(async (string path) =>
        {
            BackupUtils.RestoreFile(path, Program.ConversationPath);
            Console.WriteLine("restored");
            await Task.CompletedTask;
        }, restoreConvArg);

        // backup-tasks
        var backupTasksCmd = new Command("backup-tasks", "Backup tasks file");
        backupTasksCmd.SetHandler(async () =>
        {
            var dest = BackupUtils.BackupFile(Program.TasksPath, "tasks.json");
            Console.WriteLine(dest);
            await Task.CompletedTask;
        });

        // restore-tasks
        var restoreTasksArg = new Argument<string>("path");
        var restoreTasksCmd = new Command("restore-tasks", "Restore tasks from backup") { restoreTasksArg };
        restoreTasksCmd.SetHandler(async (string path) =>
        {
            BackupUtils.RestoreFile(path, Program.TasksPath);
            Console.WriteLine("restored");
            await Task.CompletedTask;
        }, restoreTasksArg);

        // backup-tools
        var backupToolsCmd = new Command("backup-tools", "Backup tools file");
        backupToolsCmd.SetHandler(async () =>
        {
            var dest = BackupUtils.BackupFile(Program.ToolsPath, "tools.json");
            Console.WriteLine(dest);
            await Task.CompletedTask;
        });

        // restore-tools
        var restoreToolsArg = new Argument<string>("path");
        var restoreToolsCmd = new Command("restore-tools", "Restore tools from backup") { restoreToolsArg };
        restoreToolsCmd.SetHandler(async (string path) =>
        {
            BackupUtils.RestoreFile(path, Program.ToolsPath);
            Console.WriteLine("restored");
            await Task.CompletedTask;
        }, restoreToolsArg);

        // backup-summaries
        var backupSummCmd = new Command("backup-summaries", "Backup summaries file");
        backupSummCmd.SetHandler(async () =>
        {
            var dest = BackupUtils.BackupFile(Program.SummariesPath, "summaries.json");
            Console.WriteLine(dest);
            await Task.CompletedTask;
        });

        // restore-summaries
        var restoreSummArg = new Argument<string>("path");
        var restoreSummCmd = new Command("restore-summaries", "Restore summaries from backup") { restoreSummArg };
        restoreSummCmd.SetHandler(async (string path) =>
        {
            BackupUtils.RestoreFile(path, Program.SummariesPath);
            Console.WriteLine("restored");
            await Task.CompletedTask;
        }, restoreSummArg);

        // backup-lsp
        var backupLspCmd = new Command("backup-lsp", "Backup LSP server list");
        backupLspCmd.SetHandler(async () =>
        {
            var dest = BackupUtils.BackupFile(Program.LspPath, "lsp.json");
            Console.WriteLine(dest);
            await Task.CompletedTask;
        });

        // restore-lsp
        var restoreLspArg = new Argument<string>("path");
        var restoreLspCmd = new Command("restore-lsp", "Restore LSP list from backup") { restoreLspArg };
        restoreLspCmd.SetHandler(async (string path) =>
        {
            BackupUtils.RestoreFile(path, Program.LspPath);
            Console.WriteLine("restored");
            await Task.CompletedTask;
        }, restoreLspArg);

        // backup-all
        var backupAllCmd = new Command("backup-all", "Backup all major data files");
        backupAllCmd.SetHandler(async () =>
        {
            Console.WriteLine(BackupUtils.BackupFile(Program.StatePath, "state.json"));
            Console.WriteLine(BackupUtils.BackupFile(Program.MemoryPath, "memory.md"));
            Console.WriteLine(BackupUtils.BackupFile(Program.ConversationPath, "conversation.json"));
            Console.WriteLine(BackupUtils.BackupFile(Program.TasksPath, "tasks.json"));
            Console.WriteLine(BackupUtils.BackupFile(Program.SummariesPath, "summaries.json"));
            Console.WriteLine(BackupUtils.BackupFile(Program.ToolsPath, "tools.json"));
            Console.WriteLine(BackupUtils.BackupFile(Program.LspPath, "lsp.json"));
            await Task.CompletedTask;
        });

        // list-backups
        var listBackupsCmd = new Command("list-backups", "List backup files");
        listBackupsCmd.SetHandler(async () =>
        {
            if (Directory.Exists(BackupUtils.BackupDir))
            {
                foreach (var file in Directory.GetFiles(BackupUtils.BackupDir)) Console.WriteLine(file);
            }
            await Task.CompletedTask;
        });

        // open-latest-backup
        var openLatestBackupCmd = new Command("open-latest-backup", "Open newest backup file");
        openLatestBackupCmd.SetHandler(async () =>
        {
            var path = BackupUtils.LatestBackup();
            if (path != null && File.Exists(path))
            {
                var psi = new ProcessStartInfo(path) { UseShellExecute = true };
                Process.Start(psi);
            }
            else Console.WriteLine("no backup");
            await Task.CompletedTask;
        });

        // tasks-by-priority
        var tasksByPriorityCmd = new Command("tasks-by-priority", "List tasks sorted by priority");
        tasksByPriorityCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var t in state.Tasks.OrderByDescending(t => t.Priority))
                Console.WriteLine($"{t.Priority}: {t.Id} {t.Description}");
            await Task.CompletedTask;
        });

        // conversation-insert
        var insertIdxOpt = new Option<int>("--index") { IsRequired = true };
        var insertTextOpt = new Option<string>("--text") { IsRequired = true };
        var convInsertCmd = new Command("conversation-insert", "Insert message at index") { insertIdxOpt, insertTextOpt };
        convInsertCmd.SetHandler(async (int index, string text) =>
        {
            var state = Program.LoadState();
            if (index < 0 || index > state.Conversation.Count)
            {
                Console.WriteLine("Invalid index");
                return;
            }
            state.Conversation.Insert(index, text);
            Program.SaveState(state);
            Console.WriteLine("Inserted");
            await Task.CompletedTask;
        }, insertIdxOpt, insertTextOpt);

        // conversation-replace
        var replaceIdxOpt = new Option<int>("--index") { IsRequired = true };
        var replaceTextOpt = new Option<string>("--text") { IsRequired = true };
        var convReplaceCmd = new Command("conversation-replace", "Replace message at index") { replaceIdxOpt, replaceTextOpt };
        convReplaceCmd.SetHandler(async (int index, string text) =>
        {
            var state = Program.LoadState();
            if (index < 0 || index >= state.Conversation.Count)
            {
                Console.WriteLine("Invalid index");
                return;
            }
            state.Conversation[index] = text;
            Program.SaveState(state);
            Console.WriteLine("Replaced");
            await Task.CompletedTask;
        }, replaceIdxOpt, replaceTextOpt);

        // conversation-move
        var moveFromOpt = new Option<int>("--from") { IsRequired = true };
        var moveToOpt = new Option<int>("--to") { IsRequired = true };
        var convMoveCmd = new Command("conversation-move", "Move message to new index") { moveFromOpt, moveToOpt };
        convMoveCmd.SetHandler(async (int from, int to) =>
        {
            var state = Program.LoadState();
            if (from < 0 || from >= state.Conversation.Count || to < 0 || to > state.Conversation.Count)
            {
                Console.WriteLine("Invalid index");
                return;
            }
            var item = state.Conversation[from];
            state.Conversation.RemoveAt(from);
            state.Conversation.Insert(to, item);
            Program.SaveState(state);
            Console.WriteLine("Moved");
            await Task.CompletedTask;
        }, moveFromOpt, moveToOpt);

        // conversation-role-count
        var convRoleCountCmd = new Command("conversation-role-count", "Count messages by role");
        convRoleCountCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            int user = 0, assistant = 0, system = 0;
            foreach (var line in state.Conversation)
            {
                if (line.StartsWith("[user]") || line.StartsWith("User:")) user++;
                else if (line.StartsWith("[assistant]") || line.StartsWith("Assistant:")) assistant++;
                else if (line.StartsWith("[system]") || line.StartsWith("System:")) system++;
            }
            Console.WriteLine($"user:{user} assistant:{assistant} system:{system}");
            await Task.CompletedTask;
        });

        // conversation-first
        var conversationFirstCmd = new Command("conversation-first", "Show the first message");
        conversationFirstCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            Console.WriteLine(state.Conversation.FirstOrDefault() ?? "none");
            await Task.CompletedTask;
        });

        // conversation-last
        var conversationLastCmd = new Command("conversation-last", "Show the last message");
        conversationLastCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            Console.WriteLine(state.Conversation.Count > 0 ? state.Conversation[^1] : "none");
            await Task.CompletedTask;
        });

        // conversation-range
        var rangeStartArg = new Argument<int>("start");
        var rangeEndArg = new Argument<int>("end");
        var conversationRangeCmd = new Command("conversation-range", "List messages in range") { rangeStartArg, rangeEndArg };
        conversationRangeCmd.SetHandler(async (int start, int end) =>
        {
            var state = Program.LoadState();
            if (start < 0 || end < start || end >= state.Conversation.Count) { Console.WriteLine("invalid range"); return; }
            for (int i = start; i <= end; i++) Console.WriteLine(state.Conversation[i]);
            await Task.CompletedTask;
        }, rangeStartArg, rangeEndArg);

        // delete-conversation-range
        var delRangeStartArg = new Argument<int>("start");
        var delRangeEndArg = new Argument<int>("end");
        var deleteConvRangeCmd = new Command("delete-conversation-range", "Delete messages in a range") { delRangeStartArg, delRangeEndArg };
        deleteConvRangeCmd.SetHandler(async (int start, int end) =>
        {
            var state = Program.LoadState();
            if (start < 0 || end < start || end >= state.Conversation.Count) { Console.WriteLine("invalid range"); return; }
            state.Conversation.RemoveRange(start, end - start + 1);
            Program.SaveState(state);
            Console.WriteLine("deleted");
            await Task.CompletedTask;
        }, delRangeStartArg, delRangeEndArg);

        // conversation-info
        var conversationInfoCmd = new Command("conversation-info", "Show message and char counts");
        conversationInfoCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var chars = state.Conversation.Sum(m => m.Length);
            Console.WriteLine($"messages:{state.Conversation.Count} chars:{chars}");
            await Task.CompletedTask;
        });

        // conversation-length
        var conversationLengthCmd = new Command("conversation-length", "Show number of messages");
        conversationLengthCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            Console.WriteLine(state.Conversation.Count);
            await Task.CompletedTask;
        });

        // conversation-search
        var convSearchArg = new Argument<string>("text");
        var conversationSearchCmd = new Command("conversation-search", "Search conversation for text") { convSearchArg };
        conversationSearchCmd.SetHandler(async (string text) =>
        {
            var state = Program.LoadState();
            foreach (var (line, idx) in state.Conversation.Select((l,i)=>(l,i)))
                if (line.Contains(text, StringComparison.OrdinalIgnoreCase))
                    Console.WriteLine($"{idx}: {line}");
            await Task.CompletedTask;
        }, convSearchArg);

        // list-conversation
        var listConversationCmd = new Command("list-conversation", "List conversation with indexes");
        listConversationCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            for (int i = 0; i < state.Conversation.Count; i++) Console.WriteLine($"{i}: {state.Conversation[i]}");
            await Task.CompletedTask;
        });

        // conversation-at
        var atIndexArg = new Argument<int>("index");
        var conversationAtCmd = new Command("conversation-at", "Show message at index") { atIndexArg };
        conversationAtCmd.SetHandler(async (int index) =>
        {
            var state = Program.LoadState();
            if (index < 0 || index >= state.Conversation.Count) { Console.WriteLine("invalid index"); return; }
            Console.WriteLine(state.Conversation[index]);
            await Task.CompletedTask;
        }, atIndexArg);

        // delete-conversation-before
        var beforeArg = new Argument<int>("index");
        var deleteBeforeCmd = new Command("delete-conversation-before", "Remove messages before index") { beforeArg };
        deleteBeforeCmd.SetHandler(async (int index) =>
        {
            var state = Program.LoadState();
            if (index < 0 || index >= state.Conversation.Count) { Console.WriteLine("invalid index"); return; }
            state.Conversation = state.Conversation.Skip(index).ToList();
            Program.SaveState(state);
            Console.WriteLine("removed");
            await Task.CompletedTask;
        }, beforeArg);

        // delete-conversation-after
        var afterArg = new Argument<int>("index");
        var deleteAfterCmd = new Command("delete-conversation-after", "Remove messages after index") { afterArg };
        deleteAfterCmd.SetHandler(async (int index) =>
        {
            var state = Program.LoadState();
            if (index < -1 || index >= state.Conversation.Count) { Console.WriteLine("invalid index"); return; }
            state.Conversation = state.Conversation.Take(index + 1).ToList();
            Program.SaveState(state);
            Console.WriteLine("truncated");
            await Task.CompletedTask;
        }, afterArg);

        // delete-conversation-contains
        var deleteContainsArg = new Argument<string>("text");
        var deleteContainsCmd = new Command("delete-conversation-contains", "Remove messages containing text") { deleteContainsArg };
        deleteContainsCmd.SetHandler(async (string text) =>
        {
            var state = Program.LoadState();
            int before = state.Conversation.Count;
            state.Conversation = state.Conversation.Where(m => !m.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
            Program.SaveState(state);
            Console.WriteLine(before - state.Conversation.Count);
            await Task.CompletedTask;
        }, deleteContainsArg);

        // reverse-conversation
        var reverseConvCmd = new Command("reverse-conversation", "Reverse message order");
        reverseConvCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            state.Conversation.Reverse();
            Program.SaveState(state);
            Console.WriteLine("reversed");
            await Task.CompletedTask;
        });

        // conversation-diff
        var diffFileArg = new Argument<string>("path");
        var conversationDiffCmd = new Command("conversation-diff", "Diff conversation with file") { diffFileArg };
        conversationDiffCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("file not found"); return; }
            var state = Program.LoadState();
            var diff = Program.GenerateDiff(File.ReadAllText(path), string.Join("\n", state.Conversation));
            Console.WriteLine(diff);
            await Task.CompletedTask;
        }, diffFileArg);

        // memory-sort
        var memorySortCmd = new Command("sort-memory", "Sort memory sections alphabetically");
        memorySortCmd.SetHandler(async () =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("No memory file"); return; }
            var lines = File.ReadAllLines(Program.MemoryPath);
            var sections = new SortedDictionary<string, List<string>>();
            string current = string.Empty;
            foreach (var l in lines)
            {
                if (l.StartsWith("## "))
                {
                    current = l.Substring(3).Trim();
                    sections[current] = new List<string>();
                }
                else if (!string.IsNullOrWhiteSpace(current))
                {
                    sections[current].Add(l);
                }
            }
            var sb = new StringBuilder();
            foreach (var kv in sections)
            {
                sb.AppendLine($"## {kv.Key}");
                foreach (var l in kv.Value) sb.AppendLine(l);
            }
            File.WriteAllText(Program.MemoryPath, sb.ToString());
            Console.WriteLine("sorted");
            await Task.CompletedTask;
        });

        // search-memory-regex
        var regexArg = new Argument<string>("pattern");
        var searchMemoryRegexCmd = new Command("search-memory-regex", "Regex search in memory") { regexArg };
        searchMemoryRegexCmd.SetHandler(async (string pattern) =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("No memory file"); return; }
            var regex = new System.Text.RegularExpressions.Regex(pattern);
            foreach (var (line, idx) in File.ReadAllLines(Program.MemoryPath).Select((l, i) => (l, i + 1)))
                if (regex.IsMatch(line)) Console.WriteLine($"{idx}:{line}");
            await Task.CompletedTask;
        }, regexArg);

        // memory-word-frequency
        var topOpt = new Option<int>("--top", () => 10);
        var memoryFreqCmd = new Command("memory-word-frequency", "Top N words in memory") { topOpt };
        memoryFreqCmd.SetHandler(async (int top) =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("No memory file"); return; }
            var words = File.ReadAllText(Program.MemoryPath)
                .Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.ToLowerInvariant());
            var freq = words.GroupBy(w => w).Select(g => (Word: g.Key, Count: g.Count()))
                .OrderByDescending(x => x.Count).Take(top);
            foreach (var (word, count) in freq) Console.WriteLine($"{word}:{count}");
            await Task.CompletedTask;
        }, topOpt);

        // tasks-by-created
        var tasksByCreatedCmd = new Command("tasks-by-created", "List tasks sorted by creation time");
        tasksByCreatedCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var t in state.Tasks.OrderBy(t => t.CreatedAt))
                Console.WriteLine($"{t.CreatedAt:u} {t.Id} {t.Description}");
            await Task.CompletedTask;
        });

        // reset-tasks
        var resetTasksCmd = new Command("reset-tasks", "Clear tasks and reset current task");
        resetTasksCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            state.Tasks.Clear();
            state.CurrentTaskId = null;
            Program.SaveState(state);
            Console.WriteLine("tasks reset");
            await Task.CompletedTask;
        });

        // export-tasks-csv
        var exportCsvArg = new Argument<string>("path");
        var exportTasksCsvCmd = new Command("export-tasks-csv", "Export tasks to CSV") { exportCsvArg };
        exportTasksCsvCmd.SetHandler(async (string path) =>
        {
            var state = Program.LoadState();
            var sb = new StringBuilder();
            sb.AppendLine("id,description,status,created_at,updated_at,tool_count,input_tokens,output_tokens,priority");
            foreach (var t in state.Tasks)
            {
                sb.AppendLine($"{t.Id},{EscapeCsv(t.Description)},{t.Status},{t.CreatedAt:u},{t.UpdatedAt:u},{t.ToolCount},{t.InputTokens},{t.OutputTokens},{t.Priority}");
            }
            File.WriteAllText(path, sb.ToString());
            Console.WriteLine($"exported to {path}");
            await Task.CompletedTask;
        }, exportCsvArg);

        // import-tasks-csv
        var importCsvArg = new Argument<string>("path");
        var importTasksCsvCmd = new Command("import-tasks-csv", "Import tasks from CSV") { importCsvArg };
        importTasksCsvCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("file not found"); return; }
            var lines = File.ReadAllLines(path).Skip(1);
            var list = new List<TaskRecord>();
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length < 9) continue;
                list.Add(new TaskRecord
                {
                    Id = parts[0],
                    Description = parts[1],
                    Status = parts[2],
                    CreatedAt = DateTime.Parse(parts[3]),
                    UpdatedAt = DateTime.Parse(parts[4]),
                    ToolCount = int.Parse(parts[5]),
                    InputTokens = int.Parse(parts[6]),
                    OutputTokens = int.Parse(parts[7]),
                    Priority = int.Parse(parts[8])
                });
            }
            var state = Program.LoadState();
            state.Tasks = list;
            Program.SaveState(state);
            Console.WriteLine("imported");
            await Task.CompletedTask;
        }, importCsvArg);

        // conversation-insert already added above

        // open-state
        var openStateCmd = new Command("open-state", "Open state.json in default editor");
        openStateCmd.SetHandler(async () =>
        {
            var psi = new ProcessStartInfo(Program.StatePath) { UseShellExecute = true };
            Process.Start(psi);
            await Task.CompletedTask;
        });

        // task-summary
        var taskSummaryCmd = new Command("task-summary", "Show counts of tasks by status");
        taskSummaryCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var groups = state.Tasks.GroupBy(t => t.Status)
                .Select(g => ($"{g.Key}", g.Count()));
            foreach (var (status, count) in groups)
                Console.WriteLine($"{status}:{count}");
            await Task.CompletedTask;
        });

        // delete-tasks-by-status
        var delStatusArg = new Argument<string>("status");
        var deleteByStatusCmd = new Command("delete-tasks-by-status", "Remove all tasks with a given status") { delStatusArg };
        deleteByStatusCmd.SetHandler(async (string status) =>
        {
            var state = Program.LoadState();
            int before = state.Tasks.Count;
            state.Tasks = state.Tasks.Where(t => !string.Equals(t.Status, status, StringComparison.OrdinalIgnoreCase)).ToList();
            Program.SaveState(state);
            Console.WriteLine($"removed {before - state.Tasks.Count}");
            await Task.CompletedTask;
        }, delStatusArg);

        // list-memory-files
        var listMemFilesCmd = new Command("list-memory-files", "List memory and backup files");
        listMemFilesCmd.SetHandler(async () =>
        {
            if (File.Exists(Program.MemoryPath)) Console.WriteLine(Program.MemoryPath);
            if (Directory.Exists(BackupUtils.BackupDir))
            {
                foreach (var f in Directory.GetFiles(BackupUtils.BackupDir, "memory*"))
                    Console.WriteLine(f);
            }
            await Task.CompletedTask;
        });

        // memory-keywords
        var keywordsTopOpt = new Option<int>("--top", () => 10);
        var memKeywordsCmd = new Command("memory-keywords", "Top keywords in memory") { keywordsTopOpt };
        memKeywordsCmd.SetHandler(async (int top) =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("No memory file"); return; }
            var text = File.ReadAllText(Program.MemoryPath).ToLowerInvariant();
            var words = Regex.Matches(text, "[a-zA-Z]+")
                .Select(m => m.Value)
                .Where(w => !StopWords.Contains(w));
            var freq = words.GroupBy(w => w).Select(g => (Word: g.Key, Count: g.Count()))
                .OrderByDescending(g => g.Count).Take(top);
            foreach (var (word, count) in freq) Console.WriteLine($"{word}:{count}");
            await Task.CompletedTask;
        }, keywordsTopOpt);

        // conversation-to-md
        var convMdArg = new Argument<string>("path");
        var convToMdCmd = new Command("conversation-to-md", "Export conversation as Markdown") { convMdArg };
        convToMdCmd.SetHandler(async (string path) =>
        {
            var state = Program.LoadState();
            var sb = new StringBuilder();
            foreach (var line in state.Conversation)
            {
                var content = line;
                string prefix = "";
                if (line.StartsWith("[user]") || line.StartsWith("User:")) prefix = "**User:** ";
                else if (line.StartsWith("[assistant]") || line.StartsWith("Assistant:")) prefix = "**Assistant:** ";
                else if (line.StartsWith("[system]") || line.StartsWith("System:")) prefix = "**System:** ";
                content = line.Split(']', 2).Last().Trim();
                sb.AppendLine(prefix + content);
            }
            await File.WriteAllTextAsync(path, sb.ToString());
        }, convMdArg);

        // open-latest-tool
        var openLatestToolCmd = new Command("open-latest-tool", "Open file associated with the latest tool run");
        openLatestToolCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var tool = state.ToolExecutions.OrderByDescending(t => t.StartTime).FirstOrDefault();
            if (tool?.Metadata != null && tool.Metadata.TryGetValue("filePath", out var pathObj) && pathObj is string path && File.Exists(path))
            {
                var psi = new ProcessStartInfo(path) { UseShellExecute = true };
                Process.Start(psi);
            }
            else Console.WriteLine("no file");
            await Task.CompletedTask;
        });

        // tool-log
        var toolLogIdArg = new Argument<string>("id");
        var toolLogCmd = new Command("tool-log", "Show progress messages for a tool") { toolLogIdArg };
        toolLogCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var tool = state.ToolExecutions.FirstOrDefault(t => t.Id == id);
            if (tool == null) { Console.WriteLine("not found"); return; }
            Console.WriteLine(tool.Message);
            await Task.CompletedTask;
        }, toolLogIdArg);

        // task-notes-exists
        var notesExistsArg = new Argument<string>("id");
        var notesExistsCmd = new Command("task-notes-exists", "Check if a task has notes") { notesExistsArg };
        notesExistsCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var task = state.Tasks.FirstOrDefault(t => t.Id == id);
            Console.WriteLine(!string.IsNullOrWhiteSpace(task?.Notes) ? "true" : "false");
            await Task.CompletedTask;
        }, notesExistsArg);

        // log-errors
        var logErrLinesOpt = new Option<int>("--lines", () => 20);
        var logErrorsCmd = new Command("log-errors", "Show last error lines from log") { logErrLinesOpt };
        logErrorsCmd.SetHandler(async (int lines) =>
        {
            foreach (var line in LogUtils.SearchLog("ERROR").TakeLast(lines))
                Console.WriteLine(line);
            await Task.CompletedTask;
        }, logErrLinesOpt);

        // state-diff
        var stateDiffArg = new Argument<string>("path");
        var stateDiffCmd = new Command("state-diff", "Diff current state with another") { stateDiffArg };
        stateDiffCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path) || !File.Exists(Program.StatePath)) { Console.WriteLine("file missing"); return; }
            var diff = Program.GenerateDiff(File.ReadAllText(Program.StatePath), File.ReadAllText(path));
            Console.WriteLine(diff);
            await Task.CompletedTask;
        }, stateDiffArg);

        // conversation-to-jsonl
        var convJsonlArg = new Argument<string>("path");
        var convToJsonlCmd = new Command("conversation-to-jsonl", "Export conversation to JSONL") { convJsonlArg };
        convToJsonlCmd.SetHandler(async (string path) =>
        {
            var state = Program.LoadState();
            using var writer = new StreamWriter(path);
            foreach (var line in state.Conversation)
            {
                string role = line.StartsWith("[user]") || line.StartsWith("User:") ? "user" :
                              line.StartsWith("[assistant]") || line.StartsWith("Assistant:") ? "assistant" :
                              line.StartsWith("[system]") || line.StartsWith("System:") ? "system" : "unknown";
                string content = line.Contains(']') ? line.Split(']', 2)[1].Trim() : line;
                var obj = new { role, content };
                await writer.WriteLineAsync(JsonSerializer.Serialize(obj));
            }
        }, convJsonlArg);

        // conversation-from-jsonl
        var convFromJsonlArg = new Argument<string>("path");
        var convFromJsonlCmd = new Command("conversation-from-jsonl", "Import conversation from JSONL") { convFromJsonlArg };
        convFromJsonlCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("file not found"); return; }
            var lines = new List<string>();
            foreach (var json in File.ReadLines(path))
            {
                var doc = JsonDocument.Parse(json).RootElement;
                var role = doc.GetProperty("role").GetString();
                var content = doc.GetProperty("content").GetString();
                lines.Add($"[{role}] {content}");
            }
            var state = Program.LoadState();
            state.Conversation = lines;
            Program.SaveState(state);
            Console.WriteLine("loaded");
            await Task.CompletedTask;
        }, convFromJsonlArg);

        // memory-line-count
        var memLineCountCmd = new Command("memory-line-count", "Number of lines in memory file");
        memLineCountCmd.SetHandler(async () =>
        {
            int count = File.Exists(Program.MemoryPath) ? File.ReadAllLines(Program.MemoryPath).Length : 0;
            Console.WriteLine(count);
            await Task.CompletedTask;
        });

        // tasks-with-notes
        var tasksWithNotesCmd = new Command("tasks-with-notes", "List tasks containing notes");
        tasksWithNotesCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var t in state.Tasks.Where(t => !string.IsNullOrWhiteSpace(t.Notes)))
                Console.WriteLine($"{t.Id}: {t.Description}");
            await Task.CompletedTask;
        });

        // tasks-without-due
        var tasksWithoutDueCmd = new Command("tasks-without-due", "List tasks missing due date");
        tasksWithoutDueCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var t in state.Tasks.Where(t => t.DueDate == null))
                Console.WriteLine($"{t.Id}: {t.Description}");
            await Task.CompletedTask;
        });

        // tasks-without-tags
        var tasksWithoutTagsCmd = new Command("tasks-without-tags", "List tasks that have no tags");
        tasksWithoutTagsCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var t in state.Tasks.Where(t => t.Tags.Count == 0))
                Console.WriteLine($"{t.Id}: {t.Description}");
            await Task.CompletedTask;
        });

        // add-memory-section
        var addSecNameArg = new Argument<string>("section");
        var addSecFileArg = new Argument<string>("path");
        var addMemSectionCmd = new Command("add-memory-section", "Add new memory section from file") { addSecNameArg, addSecFileArg };
        addMemSectionCmd.SetHandler(async (string section, string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("file not found"); return; }
            var lines = File.ReadAllLines(path);
            using var sw = new StreamWriter(Program.MemoryPath, append: true);
            await sw.WriteLineAsync($"## {section}");
            foreach (var l in lines) await sw.WriteLineAsync(l);
            Console.WriteLine("section added");
        }, addSecNameArg, addSecFileArg);

        // update-memory-section
        var updSecNameArg = new Argument<string>("section");
        var updSecFileArg = new Argument<string>("path");
        var updateMemSectionCmd = new Command("update-memory-section", "Replace memory section with file contents") { updSecNameArg, updSecFileArg };
        updateMemSectionCmd.SetHandler(async (string section, string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("file not found"); return; }
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("no memory file"); return; }
            var lines = File.ReadAllLines(Program.MemoryPath).ToList();
            int start = lines.FindIndex(l => l.Trim() == $"## {section}");
            if (start == -1) { Console.WriteLine("section not found"); return; }
            int end = lines.FindIndex(start + 1, l => l.StartsWith("## "));
            if (end == -1) end = lines.Count;
            var newLines = File.ReadAllLines(path);
            var result = new List<string>(lines.Take(start + 1));
            result.AddRange(newLines);
            result.AddRange(lines.Skip(end));
            File.WriteAllLines(Program.MemoryPath, result);
            Console.WriteLine("section updated");
            await Task.CompletedTask;
        }, updSecNameArg, updSecFileArg);

        // conversation-clear-after
        var clearAfterArg = new Argument<int>("index");
        var convClearAfterCmd = new Command("conversation-clear-after", "Remove messages after index") { clearAfterArg };
        convClearAfterCmd.SetHandler(async (int index) =>
        {
            var state = Program.LoadState();
            if (index < 0 || index >= state.Conversation.Count) { Console.WriteLine("invalid index"); return; }
            state.Conversation = state.Conversation.Take(index + 1).ToList();
            Program.SaveState(state);
            Console.WriteLine("truncated");
            await Task.CompletedTask;
        }, clearAfterArg);

        // conversation-slice
        var sliceStartArg = new Argument<int>("start");
        var sliceEndArg = new Argument<int>("end");
        var convSliceCmd = new Command("conversation-slice", "Show messages in range") { sliceStartArg, sliceEndArg };
        convSliceCmd.SetHandler(async (int start, int end) =>
        {
            var state = Program.LoadState();
            if (start < 0 || end < start || end >= state.Conversation.Count) { Console.WriteLine("invalid range"); return; }
            for (int i = start; i <= end; i++) Console.WriteLine(state.Conversation[i]);
            await Task.CompletedTask;
        }, sliceStartArg, sliceEndArg);

        // next-task
        var nextTaskCmd = new Command("next-task", "Show next pending task by priority");
        nextTaskCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var next = state.Tasks.Where(t => t.Status == "in-progress" || t.Status == "pending")
                .OrderByDescending(t => t.Priority).ThenBy(t => t.CreatedAt).FirstOrDefault();
            Console.WriteLine(next != null ? next.Id : "none");
            await Task.CompletedTask;
        });

        root.Add(listCmd);
        root.Add(fileWritable);
        root.Add(dirWritable);
        root.Add(dirSize);
        root.Add(memoryStats);
        root.Add(memoryUnique);
        root.Add(showConfigCmd);
        root.Add(estimateTokensCmd);
        root.Add(extractMetaCmd);
        root.Add(toolDescCmd);
        root.Add(hasActiveCmd);
        root.Add(taskStatusesCmd);
        root.Add(validateKeyCmd);
        root.Add(determineProviderCmd);
        root.Add(displayToSessionCmd);
        root.Add(sessionToDisplayCmd);
        root.Add(summarizeTextCmd);
        root.Add(summarizeFileCmd);
        root.Add(summarizeMemorySectionCmd);
        root.Add(summarizeTasksCmd);
        root.Add(summarizeStateCmd);
        root.Add(conversationWordFreqCmd);
        root.Add(conversationUniqueCmd);
        root.Add(logPathCmd);
        root.Add(searchLogCmd);
        root.Add(exportLogCmd);
        root.Add(backupStateCmd);
        root.Add(restoreStateCmd);
        root.Add(backupMemoryCmd);
        root.Add(restoreMemoryCmd);
        root.Add(backupConvCmd);
        root.Add(restoreConvCmd);
        root.Add(backupTasksCmd);
        root.Add(restoreTasksCmd);
        root.Add(backupToolsCmd);
        root.Add(restoreToolsCmd);
        root.Add(backupSummCmd);
        root.Add(restoreSummCmd);
        root.Add(backupLspCmd);
        root.Add(restoreLspCmd);
        root.Add(backupAllCmd);
        root.Add(listBackupsCmd);
        root.Add(openLatestBackupCmd);
        root.Add(tasksByTagCmd);
        root.Add(tasksTagSearchCmd);
        root.Add(tasksByPriorityCmd);
        root.Add(conversationFirstCmd);
        root.Add(conversationRangeCmd);
        root.Add(conversationInfoCmd);
        root.Add(listConversationCmd);
        root.Add(conversationAtCmd);
        root.Add(deleteBeforeCmd);
        root.Add(deleteAfterCmd);
        root.Add(deleteContainsCmd);
        root.Add(reverseConvCmd);
        root.Add(conversationDiffCmd);
        root.Add(conversationLastCmd);
        root.Add(conversationLengthCmd);
        root.Add(conversationSearchCmd);
        root.Add(deleteConvRangeCmd);
        root.Add(convReplaceCmd);
        root.Add(convMoveCmd);
        root.Add(convRoleCountCmd);
        root.Add(memorySortCmd);
        root.Add(searchMemoryRegexCmd);
        root.Add(memoryFreqCmd);
        root.Add(tasksByCreatedCmd);
        root.Add(resetTasksCmd);
        root.Add(exportTasksCsvCmd);
        root.Add(importTasksCsvCmd);
        root.Add(convInsertCmd);
        root.Add(openStateCmd);
        root.Add(taskSummaryCmd);
        root.Add(deleteByStatusCmd);
        root.Add(listMemFilesCmd);
        root.Add(memKeywordsCmd);
        root.Add(convToMdCmd);
        root.Add(openLatestToolCmd);
        root.Add(toolLogCmd);
        root.Add(notesExistsCmd);
        root.Add(logErrorsCmd);
        root.Add(stateDiffCmd);
        root.Add(convToJsonlCmd);
        root.Add(convFromJsonlCmd);
        root.Add(memLineCountCmd);
        root.Add(tasksTodayCmd);
        root.Add(tasksNextWeekCmd);
        root.Add(tasksThisMonthCmd);
        root.Add(pauseTaskCmd);
        root.Add(resumeTaskCmd);
        root.Add(pauseAllCmd);
        root.Add(resumeAllCmd);
        root.Add(tasksPausedCmd);
        root.Add(tasksCreatedBeforeCmd);
        root.Add(tasksCreatedAfterCmd);
        root.Add(archiveTaskCmd);
        root.Add(unarchiveTaskCmd);
        root.Add(archivedTasksCmd);
        root.Add(listTaskIdsCmd);
        root.Add(tasksWithNotesCmd);
        root.Add(tasksWithoutDueCmd);
        root.Add(tasksWithoutTagsCmd);
        root.Add(addMemSectionCmd);
        root.Add(updateMemSectionCmd);
        root.Add(convClearAfterCmd);
        root.Add(convSliceCmd);
        root.Add(nextTaskCmd);
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

    static string EscapeCsv(string s)
    {
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
        {
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
        return s;
    }
}
