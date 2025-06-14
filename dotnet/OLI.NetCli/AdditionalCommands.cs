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

        // tasks-by-priority
        var tasksByPriorityCmd = new Command("tasks-by-priority", "List tasks sorted by priority");
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

        root.Add(convReplaceCmd);
        root.Add(convMoveCmd);
        root.Add(convRoleCountCmd);
        root.Add(memorySortCmd);
        root.Add(searchMemoryRegexCmd);
        root.Add(memoryFreqCmd);
        root.Add(tasksByCreatedCmd);
        root.Add(resetTasksCmd);
        root.Add(exportTasksCsvCmd);
        // json-merge
        var jsonAArg = new Argument<string>("first");
        var jsonBArg = new Argument<string>("second");
        var jsonOutArg = new Argument<string>("output");
        var jsonMergeCmd = new Command("json-merge", "Merge two JSON files") { jsonAArg, jsonBArg, jsonOutArg };
        jsonMergeCmd.SetHandler(async (string first, string second, string output) =>
        {
            if (!File.Exists(first) || !File.Exists(second))
            {
                Console.WriteLine("file not found");
                return;
            }
            try
            {
                var jsonA = JsonDocument.Parse(File.ReadAllText(first)).RootElement.Clone();
                var jsonB = JsonDocument.Parse(File.ReadAllText(second)).RootElement.Clone();
                using var stream = new MemoryStream();
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    foreach (var prop in jsonA.EnumerateObject()) prop.WriteTo(writer);
                    foreach (var prop in jsonB.EnumerateObject()) prop.WriteTo(writer);
                    writer.WriteEndObject();
                }
                File.WriteAllBytes(output, stream.ToArray());
                Console.WriteLine($"merged to {output}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"error: {ex.Message}");
            }
            await Task.CompletedTask;
        }, jsonAArg, jsonBArg, jsonOutArg);

        // json-validate
        var jsonValidateArg = new Argument<string>("path");
        var jsonValidateCmd = new Command("json-validate", "Validate JSON file") { jsonValidateArg };
        jsonValidateCmd.SetHandler(async (string path) =>
        {
            try
            {
                JsonDocument.Parse(File.ReadAllText(path));
                Console.WriteLine("valid");
            }
            catch
            {
                Console.WriteLine("invalid");
            }
            await Task.CompletedTask;
        }, jsonValidateArg);

        // memory-diff
        var memDiffArg = new Argument<string>("path");
        var memDiffCmd = new Command("memory-diff", "Diff memory file with another") { memDiffArg };
        memDiffCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(Program.MemoryPath) || !File.Exists(path))
            {
                Console.WriteLine("file not found");
                return;
            }
            var diff = Program.GenerateDiff(File.ReadAllText(Program.MemoryPath), File.ReadAllText(path));
            Console.WriteLine(diff);
            await Task.CompletedTask;
        }, memDiffArg);

        // search-log-regex
        var logRegexArg = new Argument<string>("pattern");
        var searchLogRegexCmd = new Command("search-log-regex", "Regex search in log") { logRegexArg };
        searchLogRegexCmd.SetHandler(async (string pattern) =>
        {
            var regex = new System.Text.RegularExpressions.Regex(pattern);
            foreach (var line in LogUtils.SearchLog(string.Empty))
                if (regex.IsMatch(line)) Console.WriteLine(line);
            await Task.CompletedTask;
        }, logRegexArg);

        // grep-count
        var grepPathArg = new Argument<string>("path");
        var grepTextArg = new Argument<string>("text");
        var grepCountCmd = new Command("grep-count", "Count occurrences of text in file") { grepPathArg, grepTextArg };
        grepCountCmd.SetHandler(async (string path, string text) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("not found"); return; }
            var count = File.ReadLines(path).Count(l => l.Contains(text, StringComparison.OrdinalIgnoreCase));
            Console.WriteLine(count);
            await Task.CompletedTask;
        }, grepPathArg, grepTextArg);

        // tail-file-follow
        var followPathArg = new Argument<string>("path");
        var followLinesOpt = new Option<int>("--lines", () => 10);
        var tailFollowCmd = new Command("tail-file-follow", "Tail file and follow") { followPathArg, followLinesOpt };
        tailFollowCmd.SetHandler(async (string path, int lines) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("not found"); return; }
            var all = File.ReadLines(path).ToList();
            foreach (var line in all.TakeLast(lines)) Console.WriteLine(line);
            var pos = new FileInfo(path).Length;
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(pos, SeekOrigin.Begin);
            using var reader = new StreamReader(fs);
            while (true)
            {
                var newLine = await reader.ReadLineAsync();
                if (newLine != null) Console.WriteLine(newLine);
                else await Task.Delay(500);
            }
        }, followPathArg, followLinesOpt);

        // task-age
        var taskAgeArg = new Argument<string>("id");
        var taskAgeCmd = new Command("task-age", "Show age of task in hours") { taskAgeArg };
        taskAgeCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var task = state.Tasks.FirstOrDefault(t => t.Id == id);
            if (task == null) { Console.WriteLine("not found"); return; }
            var age = DateTime.UtcNow - task.CreatedAt;
            Console.WriteLine(age.TotalHours.ToString("F1"));
            await Task.CompletedTask;
        }, taskAgeArg);

        // conversation-export-text
        var convExportTxtArg = new Argument<string>("path");
        var convExportTxtCmd = new Command("export-conversation-text", "Export conversation to text file") { convExportTxtArg };
        convExportTxtCmd.SetHandler(async (string path) =>
        {
            var state = Program.LoadState();
            await File.WriteAllLinesAsync(path, state.Conversation);
            Console.WriteLine($"exported to {path}");
        }, convExportTxtArg);

        // open-tools
        var openToolsCmd = new Command("open-tools", "Open tools.json file");
        openToolsCmd.SetHandler(async () =>
        {
            var psi = new ProcessStartInfo(Program.ToolsPath) { UseShellExecute = true };
            Process.Start(psi);
            await Task.CompletedTask;
        });

        // list-tool-names
        var listToolNamesCmd = new Command("list-tool-names", "List unique tool names");
        listToolNamesCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var name in state.ToolExecutions.Select(t => t.Name).Distinct())
                Console.WriteLine(name);
            await Task.CompletedTask;
        });

        // show-log-level
        var showLogLevelCmd = new Command("show-log-level", "Display current log level");
        showLogLevelCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            Console.WriteLine(state.LogLevel);
            await Task.CompletedTask;
        });

        // tail-log
        var tailLogLinesOpt = new Option<int>("--lines", () => 20);
        var tailLogCmd = new Command("tail-log", "Show last N lines of log") { tailLogLinesOpt };
        tailLogCmd.SetHandler(async (int lines) =>
        {
            Console.WriteLine(LogUtils.ReadLog(lines));
            await Task.CompletedTask;
        }, tailLogLinesOpt);

        // cleanup-tasks
        var cleanupDaysOpt = new Option<int>("--days", () => 30, "Remove tasks older than N days");
        var cleanupTasksCmd = new Command("cleanup-tasks", "Delete tasks older than specified days") { cleanupDaysOpt };
        cleanupTasksCmd.SetHandler(async (int days) =>
        {
            var state = Program.LoadState();
            var cutoff = DateTime.UtcNow.AddDays(-days);
            state.Tasks.RemoveAll(t => t.CreatedAt < cutoff);
            Program.SaveState(state);
            Console.WriteLine($"Removed tasks older than {days} days");
            await Task.CompletedTask;
        }, cleanupDaysOpt);

        // conversation-average-length
        var convAvgCmd = new Command("conversation-average-length", "Average length of conversation messages");
        convAvgCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            if (state.Conversation.Count == 0) { Console.WriteLine("0"); return; }
            var avg = state.Conversation.Average(m => m.Length);
            Console.WriteLine(avg.ToString("F1"));
            await Task.CompletedTask;
        });

        // export-tasks-text
        var exportTextArg = new Argument<string>("path");
        var exportTasksTextCmd = new Command("export-tasks-text", "Export tasks to plain text") { exportTextArg };
        exportTasksTextCmd.SetHandler(async (string path) =>
        {
            var state = Program.LoadState();
            var lines = state.Tasks.Select(t => $"{t.Id} {t.Description}");
            await File.WriteAllLinesAsync(path, lines);
            Console.WriteLine($"exported to {path}");
        }, exportTextArg);

        // import-tasks-text
        var importTextArg = new Argument<string>("path");
        var importTasksTextCmd = new Command("import-tasks-text", "Import tasks from plain text") { importTextArg };
        importTasksTextCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("file not found"); return; }
            var lines = File.ReadAllLines(path);
            var tasks = lines.Select(l => new TaskRecord { Id = Guid.NewGuid().ToString(), Description = l, Status = "todo", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }).ToList();
            var state = Program.LoadState();
            state.Tasks.AddRange(tasks);
            Program.SaveState(state);
            Console.WriteLine($"imported {tasks.Count} tasks");
            await Task.CompletedTask;
        }, importTextArg);

        // memory-section-lines
        var memSectionOpt = new Option<string>("--section") { IsRequired = true };
        var memSectionCmd = new Command("memory-section-lines", "Show lines of a memory section") { memSectionOpt };
        memSectionCmd.SetHandler(async (string section) =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("No memory file"); return; }
            var lines = File.ReadAllLines(Program.MemoryPath);
            var collect = false;
            foreach (var line in lines)
            {
                if (line.StartsWith("## ")) { collect = line[3..].Trim() == section; continue; }
                if (collect && line.StartsWith("- ")) Console.WriteLine(line[2..]);
            }
            await Task.CompletedTask;
        }, memSectionOpt);

        // rename-memory-section
        var renameOldOpt = new Option<string>("--old") { IsRequired = true };
        var renameNewOpt = new Option<string>("--new") { IsRequired = true };
        var renameMemCmd = new Command("rename-memory-section", "Rename a memory section") { renameOldOpt, renameNewOpt };
        renameMemCmd.SetHandler(async (string oldName, string newName) =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("No memory file"); return; }
            var lines = File.ReadAllLines(Program.MemoryPath).ToList();
            var changed = false;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].StartsWith("## ") && lines[i][3..].Trim() == oldName)
                {
                    lines[i] = "## " + newName;
                    changed = true;
                }
            }
            if (changed) { File.WriteAllLines(Program.MemoryPath, lines); Console.WriteLine("renamed"); }
            else Console.WriteLine("section not found");
            await Task.CompletedTask;
        }, renameOldOpt, renameNewOpt);

        // memory-section-exists
        var memExistsCmd = new Command("memory-section-exists", "Check if memory section exists") { memSectionOpt };
        memExistsCmd.SetHandler(async (string section) =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("false"); return; }
            var exists = File.ReadLines(Program.MemoryPath).Any(l => l.StartsWith("## ") && l[3..].Trim() == section);
            Console.WriteLine(exists ? "true" : "false");
            await Task.CompletedTask;
        }, memSectionOpt);

        // search-tasks-regex
        var tasksRegexArg = new Argument<string>("pattern");
        var searchTasksRegexCmd = new Command("search-tasks-regex", "Regex search task descriptions") { tasksRegexArg };
        searchTasksRegexCmd.SetHandler(async (string pattern) =>
        {
            var regex = new System.Text.RegularExpressions.Regex(pattern);
            var state = Program.LoadState();
            foreach (var t in state.Tasks.Where(t => regex.IsMatch(t.Description)))
                Console.WriteLine($"{t.Id}: {t.Description}");
            await Task.CompletedTask;
        }, tasksRegexArg);

        // split-log
        var splitDirArg = new Argument<string>("directory");
        var splitLogCmd = new Command("split-log", "Split log file by day into directory") { splitDirArg };
        splitLogCmd.SetHandler(async (string directory) =>
        {
            if (!File.Exists(LogUtils.LogPath)) { Console.WriteLine("no log"); return; }
            Directory.CreateDirectory(directory);
            foreach (var group in File.ReadLines(LogUtils.LogPath).GroupBy(l => l.Split(' ')[0].Substring(0, 10)))
            {
                var path = Path.Combine(directory, $"{group.Key}.log");
                await File.AppendAllLinesAsync(path, group);
            }
            Console.WriteLine($"logs split to {directory}");
        }, splitDirArg);

        // compress-log
        var logZipArg = new Argument<string>("zipPath");
        var compressLogCmd = new Command("compress-log", "Compress log file to zip") { logZipArg };
        compressLogCmd.SetHandler(async (string zipPath) =>
        {
            if (!File.Exists(LogUtils.LogPath)) { Console.WriteLine("no log"); return; }
            FileUtils.CompressFile(LogUtils.LogPath, zipPath);
            Console.WriteLine($"compressed to {zipPath}");
            await Task.CompletedTask;
        }, logZipArg);

        // compress-file
        var compSrcArg = new Argument<string>("source");
        var compDstArg = new Argument<string>("zip");
        var compressFileCmd = new Command("compress-file", "Compress file to zip") { compSrcArg, compDstArg };
        compressFileCmd.SetHandler(async (string source, string zip) =>
        {
            if (!File.Exists(source)) { Console.WriteLine("file missing"); return; }
            FileUtils.CompressFile(source, zip);
            Console.WriteLine($"compressed to {zip}");
            await Task.CompletedTask;
        }, compSrcArg, compDstArg);

        // decompress-file
        var decompSrcArg = new Argument<string>("zip");
        var decompDstArg = new Argument<string>("dest");
        var decompressFileCmd = new Command("decompress-file", "Extract first entry from zip") { decompSrcArg, decompDstArg };
        decompressFileCmd.SetHandler(async (string zip, string dest) =>
        {
            if (!File.Exists(zip)) { Console.WriteLine("zip missing"); return; }
            FileUtils.DecompressFile(zip, dest);
            Console.WriteLine($"extracted to {dest}");
            await Task.CompletedTask;
        }, decompSrcArg, decompDstArg);

        root.Add(importTasksCsvCmd);

    static string EscapeCsv(string s)
    {
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
        {
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
        return s;
    }
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
        root.Add(jsonMergeCmd);
        root.Add(jsonValidateCmd);
        root.Add(memDiffCmd);
        root.Add(searchLogRegexCmd);
        root.Add(grepCountCmd);
        root.Add(tailFollowCmd);
        root.Add(taskAgeCmd);
        root.Add(convExportTxtCmd);
        root.Add(openToolsCmd);
        root.Add(listToolNamesCmd);
        root.Add(showLogLevelCmd);
        root.Add(tailLogCmd);
        root.Add(cleanupTasksCmd);
        root.Add(convAvgCmd);
        root.Add(exportTasksTextCmd);
        root.Add(importTasksTextCmd);
        root.Add(memSectionCmd);
        root.Add(renameMemCmd);
        root.Add(memExistsCmd);
        root.Add(searchTasksRegexCmd);
        root.Add(splitLogCmd);
        root.Add(compressLogCmd);
        root.Add(compressFileCmd);
        root.Add(decompressFileCmd);
        root.Add(setDueCmd);
        root.Add(taskDueCmd);
        root.Add(addTagCmd);
        root.Add(removeTagCmd);
        root.Add(listTagsCmd);
        root.Add(taskExistsCmd);
        root.Add(countTagsCmd);
        root.Add(tasksByTagCmd);
        root.Add(dueSoonCmd);
        root.Add(tasksRangeCmd);
        root.Add(tasksTomorrowCmd);
        root.Add(overdueCmd);

        // search-memory
        var memQueryArg = new Argument<string>("query");
        var memIgnoreOpt = new Option<bool>("--ignore-case", "Case insensitive");
        var searchMemCmd = new Command("search-memory", "Search memory file") { memQueryArg, memIgnoreOpt };
        searchMemCmd.SetHandler(async (string query, bool ic) =>
        {
            if (!File.Exists(Program.MemoryPath))
            {
                Console.WriteLine("No memory file");
                return;
            }
            var comp = ic ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            foreach (var (line, idx) in File.ReadAllLines(Program.MemoryPath).Select((l, i) => (l, i + 1)))
                if (line.IndexOf(query, comp) >= 0)
                    Console.WriteLine($"{idx}:{line}");
            await Task.CompletedTask;
        }, memQueryArg, memIgnoreOpt);

        // conversation-exists
        var convExistsCmd = new Command("conversation-exists", "Does conversation file exist?");
        convExistsCmd.SetHandler(async () =>
        {
            Console.WriteLine(File.Exists(Program.ConversationPath) ? "true" : "false");
            await Task.CompletedTask;
        });

        // conversation-has
        var convHasArg = new Argument<string>("query");
        var convHasCmd = new Command("conversation-has", "Check if conversation contains text") { convHasArg };
        convHasCmd.SetHandler(async (string query) =>
        {
            var lines = Program.LoadState().Conversation;
            bool found = lines.Any(l => l.Contains(query, StringComparison.OrdinalIgnoreCase));
            Console.WriteLine(found ? "true" : "false");
            await Task.CompletedTask;
        }, convHasArg);

        // tasks-failed
        var tasksFailedCmd = new Command("tasks-failed", "List failed tasks");
        tasksFailedCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var t in state.Tasks.Where(t => t.Status == "Failed"))
                Console.WriteLine($"{t.Id} {t.Description}");
            await Task.CompletedTask;
        });

        // task-status-counts
        var statusCountsCmd = new Command("task-status-counts", "Show task counts by status");
        statusCountsCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var groups = state.Tasks.GroupBy(t => t.Status).Select(g => $"{g.Key}:{g.Count()}");
            foreach (var g in groups) Console.WriteLine(g);
            await Task.CompletedTask;
        });

        // memory-sort-lines
        var sortLinesCmd = new Command("memory-sort-lines", "Sort memory file lines alphabetically");
        sortLinesCmd.SetHandler(async () =>
        {
            if (!File.Exists(Program.MemoryPath))
            {
                Console.WriteLine("No memory file");
                return;
            }
            var sorted = File.ReadAllLines(Program.MemoryPath).OrderBy(l => l).ToArray();
            File.WriteAllLines(Program.MemoryPath, sorted);
            Console.WriteLine("sorted");
            await Task.CompletedTask;
        });

        // remove-empty-conversation
        var removeEmptyCmd = new Command("remove-empty-conversation", "Delete blank conversation lines");
        removeEmptyCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            state.Conversation = state.Conversation.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            Program.SaveState(state);
            Console.WriteLine("removed");
            await Task.CompletedTask;
        });

        // tasks-recent
        var recentDaysOpt = new Option<int>("--days", () => 1, "Look back N days");
        var tasksRecentCmd = new Command("tasks-recent", "List recent tasks") { recentDaysOpt };
        tasksRecentCmd.SetHandler(async (int days) =>
        {
            var state = Program.LoadState();
            var cutoff = DateTime.UtcNow.AddDays(-days);
            foreach (var t in state.Tasks.Where(t => t.CreatedAt >= cutoff))
                Console.WriteLine($"{t.Id} {t.Description} {t.CreatedAt:u}");
            await Task.CompletedTask;
        }, recentDaysOpt);

        // tasks-pending
        var tasksPendingCmd = new Command("tasks-pending", "List tasks in progress");
        tasksPendingCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var t in state.Tasks.Where(t => t.Status == "in-progress"))
                Console.WriteLine($"{t.Id} {t.Description}");
            await Task.CompletedTask;
        });

        // tasks-success
        var tasksSuccessCmd = new Command("tasks-success", "List completed tasks");
        tasksSuccessCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var t in state.Tasks.Where(t => t.Status.StartsWith("completed")))
                Console.WriteLine($"{t.Id} {t.Description}");
            await Task.CompletedTask;
        });

        // tasks-by-updated
        var tasksByUpdatedCmd = new Command("tasks-by-updated", "List tasks by last update");
        tasksByUpdatedCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var t in state.Tasks.OrderByDescending(t => t.UpdatedAt))
                Console.WriteLine($"{t.UpdatedAt:u} {t.Id} {t.Description}");
            await Task.CompletedTask;
        });

        // list-tool-ids
        var listToolIdsCmd = new Command("list-tool-ids", "List tool execution IDs");
        listToolIdsCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var id in state.ToolExecutions.Select(te => te.Id))
                Console.WriteLine(id);
            await Task.CompletedTask;
        });

        // tool-metadata
        var toolMetaIdArg = new Argument<string>("id");
        var toolMetadataCmd = new Command("tool-metadata", "Show tool execution metadata") { toolMetaIdArg };
        toolMetadataCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var tool = state.ToolExecutions.FirstOrDefault(te => te.Id == id);
            if (tool?.Metadata != null)
            {
                foreach (var kv in tool.Metadata)
                    Console.WriteLine($"{kv.Key}: {kv.Value}");
            }
            else Console.WriteLine("not found");
            await Task.CompletedTask;
        }, toolMetaIdArg);

        // export-tool-run
        var exportToolIdArg = new Argument<string>("id");
        var exportToolPathArg = new Argument<string>("path");
        var exportToolCmd = new Command("export-tool-run", "Export tool execution to file")
        { exportToolIdArg, exportToolPathArg };
        exportToolCmd.SetHandler(async (string id, string path) =>
        {
            var state = Program.LoadState();
            var tool = state.ToolExecutions.FirstOrDefault(te => te.Id == id);
            if (tool != null)
            {
                var json = JsonSerializer.Serialize(tool, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(path, json);
                Console.WriteLine("exported");
            }
            else Console.WriteLine("tool not found");
        }, exportToolIdArg, exportToolPathArg);

        // import-tool-run
        var importToolPathArg = new Argument<string>("path");
        var importToolCmd = new Command("import-tool-run", "Import tool execution from file") { importToolPathArg };
        importToolCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("file missing"); return; }
            var json = await File.ReadAllTextAsync(path);
            var tool = JsonSerializer.Deserialize<ToolExecution>(json);
            if (tool != null)
            {
                var state = Program.LoadState();
                state.ToolExecutions.Add(tool);
                Program.SaveState(state);
                Console.WriteLine("imported");
            }
        }, importToolPathArg);

        // clear-tools
        var clearToolsCmd = new Command("clear-tools", "Remove all tool executions");
        clearToolsCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            state.ToolExecutions.Clear();
            Program.SaveState(state);
            Console.WriteLine("cleared");
            await Task.CompletedTask;
        });

        // backup-path
        var backupPathCmd = new Command("backup-path", "Show backup directory");
        backupPathCmd.SetHandler(async () =>
        {
            Console.WriteLine(BackupUtils.BackupDir);
            await Task.CompletedTask;
        });

        // open-backups
        var openBackupsCmd = new Command("open-backups", "Open backup directory");
        openBackupsCmd.SetHandler(async () =>
        {
            Process.Start("xdg-open", BackupUtils.BackupDir);
            await Task.CompletedTask;
        });

        // trim-log
        var trimLinesArg = new Argument<int>("lines");
        var trimLogCmd = new Command("trim-log", "Trim log file to N lines") { trimLinesArg };
        trimLogCmd.SetHandler(async (int lines) =>
        {
            if (!File.Exists(LogUtils.LogPath)) { Console.WriteLine("no log"); return; }
            var all = File.ReadAllLines(LogUtils.LogPath).TakeLast(lines).ToArray();
            File.WriteAllLines(LogUtils.LogPath, all);
            Console.WriteLine("trimmed");
            await Task.CompletedTask;
        }, trimLinesArg);

        root.Add(searchMemCmd);
        root.Add(convExistsCmd);
        root.Add(convHasCmd);
        root.Add(tasksFailedCmd);
        root.Add(statusCountsCmd);
        root.Add(sortLinesCmd);
        root.Add(removeEmptyCmd);
        root.Add(tasksRecentCmd);
        root.Add(tasksPendingCmd);
        root.Add(tasksSuccessCmd);
        root.Add(tasksByUpdatedCmd);
        root.Add(listToolIdsCmd);
        root.Add(toolMetadataCmd);
        root.Add(exportToolCmd);
        root.Add(importToolCmd);
        root.Add(clearToolsCmd);
        root.Add(backupPathCmd);
        root.Add(openBackupsCmd);
        root.Add(trimLogCmd);


        // conversation-unique-words
        var convUniqueCmd = new Command("conversation-unique-words", "Count unique words in conversation");
        convUniqueCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var words = state.Conversation
                .SelectMany(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Select(w => w.Trim().ToLowerInvariant());
            Console.WriteLine(words.Distinct().Count());
            await Task.CompletedTask;
        });

        // memory-dedupe-lines
        var dedupeMemCmd = new Command("memory-dedupe-lines", "Remove duplicate lines in memory");
        dedupeMemCmd.SetHandler(async () =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("No memory file"); return; }
            var lines = File.ReadAllLines(Program.MemoryPath).Distinct().ToArray();
            File.WriteAllLines(Program.MemoryPath, lines);
            Console.WriteLine("deduped");
            await Task.CompletedTask;
        });

        // grep-search-adv
        var grepAdvPattern = new Argument<string>("pattern");
        var grepAdvDir = new Option<string>("--dir", () => ".");
        var grepAdvCmd = new Command("grep-search-adv", "Regex search respecting ignore patterns") { grepAdvPattern, grepAdvDir };
        grepAdvCmd.SetHandler(async (string pattern, string dir) =>
        {
            if (!Directory.Exists(dir)) { Console.WriteLine("Directory not found"); return; }
            var regex = new System.Text.RegularExpressions.Regex(pattern);
            var ignoreDirs = new[] { ".git", "node_modules", "target", "bin", "obj", "dist" };
            var binaryExt = new[] { ".exe", ".dll", ".so", ".a", ".lib", ".pyc", ".pyo", ".class" };
            foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                if (ignoreDirs.Any(id => file.Contains(id))) continue;
                if (binaryExt.Contains(Path.GetExtension(file))) continue;
                int idx = 0;
                foreach (var line in File.ReadLines(file))
                {
                    idx++; if (regex.IsMatch(line)) Console.WriteLine($"{file}:{idx}:{line}");
                }
            }
            await Task.CompletedTask;
        }, grepAdvPattern, grepAdvDir);

        // glob-search-adv
        var globAdvPattern = new Argument<string>("pattern");
        var globAdvDir = new Option<string>("--dir", () => ".");
        var globAdvCmd = new Command("glob-search-adv", "Glob search respecting ignore patterns") { globAdvPattern, globAdvDir };
        globAdvCmd.SetHandler(async (string pattern, string dir) =>
        {
            if (!Directory.Exists(dir)) { Console.WriteLine("Directory not found"); return; }
            var ignoreDirs = new[] { ".git", "node_modules", "target", "bin", "obj", "dist" };
            foreach (var file in Directory.GetFiles(dir, pattern, SearchOption.AllDirectories))
            {
                if (ignoreDirs.Any(id => file.Contains(id))) continue;
                Console.WriteLine(file);
            }
            await Task.CompletedTask;
        }, globAdvPattern, globAdvDir);

        // rpc-stream-events
        var rpcStreamCmd = new Command("rpc-stream-events", "Stream RPC events");
        rpcStreamCmd.SetHandler(async () =>
        {
            using var client = new HttpClient();
            while (true)
            {
                var res = await client.GetAsync("http://localhost:5050/events");
                var json = await res.Content.ReadAsStringAsync();
                var events = JsonSerializer.Deserialize<List<object>>(json) ?? new();
                foreach (var ev in events) Console.WriteLine(JsonSerializer.Serialize(ev));
                await Task.Delay(1000);
            }
        });

        // tool-progress
        var toolProgId = new Argument<string>("id");
        var toolProgCmd = new Command("tool-progress", "Show progress of tool execution") { toolProgId };
        toolProgCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var tool = state.ToolExecutions.FirstOrDefault(t => t.Id == id);
            if (tool != null)
                Console.WriteLine($"{tool.Id} {tool.Status} {tool.Message}");
            else Console.WriteLine("not found");
            await Task.CompletedTask;
        }, toolProgId);

        // tool-progress-all
        var toolProgAllCmd = new Command("tool-progress-all", "Show progress of all tools");
        toolProgAllCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var t in state.ToolExecutions)
                Console.WriteLine($"{t.Id} {t.Status} {t.Message}");
            await Task.CompletedTask;
        });

        // tasks-today
        var tasksTodayCmd = new Command("tasks-today", "List tasks created today");
        tasksTodayCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var today = DateTime.UtcNow.Date;
            foreach (var t in state.Tasks.Where(t => t.CreatedAt.Date == today))
                Console.WriteLine($"{t.Id} {t.Description}");
            await Task.CompletedTask;
        });

        // tasks-week
        var tasksWeekCmd = new Command("tasks-week", "List tasks from the last 7 days");
        tasksWeekCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var cutoff = DateTime.UtcNow.Date.AddDays(-7);
            foreach (var t in state.Tasks.Where(t => t.CreatedAt.Date >= cutoff))
                Console.WriteLine($"{t.Id} {t.Description} {t.CreatedAt:u}");
            await Task.CompletedTask;
        });

        // export-tasks-md
        var exportMdArg = new Argument<string>("path");
        var exportMdCmd = new Command("export-tasks-md", "Export tasks to markdown") { exportMdArg };
        exportMdCmd.SetHandler(async (string path) =>
        {
            var state = Program.LoadState();
            var sb = new StringBuilder();
            sb.AppendLine("# Tasks");
            foreach (var t in state.Tasks)
                sb.AppendLine($"- **[{t.Id}]** {t.Description} ({t.Status})");
            await File.WriteAllTextAsync(path, sb.ToString());
            Console.WriteLine("exported");
        }, exportMdArg);

        // summarize-file
        var sumFileArg = new Argument<string>("path");
        var sumFileCmd = new Command("summarize-file", "Summarize file content") { sumFileArg };
        sumFileCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }
            var text = await File.ReadAllTextAsync(path);
            try
            {
                Console.WriteLine(await SummarizeAsync(text));
            }
            catch
            {
                Console.WriteLine(Program.GenerateSummary(text));
            }
        }, sumFileArg);

        // summarize-memory-section
        var sumSecArg = new Argument<string>("section");
        var sumSecCmd = new Command("summarize-memory-section", "Summarize a memory section") { sumSecArg };
        sumSecCmd.SetHandler(async (string section) =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("No memory file"); return; }
            var lines = File.ReadAllLines(Program.MemoryPath);
            var inSec = false; var sb = new StringBuilder();
            foreach (var line in lines)
            {
                if (line.StartsWith("[")) inSec = line.Trim('[',']') == section;
                else if (inSec) sb.AppendLine(line);
            }
            var text = sb.ToString();
            if (string.IsNullOrWhiteSpace(text)) { Console.WriteLine("section not found"); return; }
            try { Console.WriteLine(await SummarizeAsync(text)); }
            catch { Console.WriteLine(Program.GenerateSummary(text)); }
        }, sumSecArg);

        // conversation-word-frequency
        var convFreqCmd = new Command("conversation-word-frequency", "Show word frequencies in conversation");
        convFreqCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var words = state.Conversation.SelectMany(m => m.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Select(w => w.ToLowerInvariant());
            var freq = words.GroupBy(w => w).Select(g => (Word: g.Key, Count: g.Count()))
                .OrderByDescending(g => g.Count).Take(10);
            foreach (var (w,c) in freq) Console.WriteLine($"{w} {c}");
            await Task.CompletedTask;
        });

        // tasks-due-today
        var tasksTodayDueCmd = new Command("tasks-due-today", "List tasks due today");
        tasksTodayDueCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var today = DateTime.UtcNow.Date;
            foreach (var t in state.Tasks.Where(t => t.DueDate?.Date == today))
                Console.WriteLine($"{t.Id} {t.Description}");
            await Task.CompletedTask;
        });

        // summarize-tasks
        var sumTasksCmd = new Command("summarize-tasks", "Summarize all tasks");
        sumTasksCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var text = string.Join("\n", state.Tasks.Select(t => t.Description));
            if (string.IsNullOrWhiteSpace(text)) { Console.WriteLine("no tasks"); return; }
            try { Console.WriteLine(await SummarizeAsync(text)); }
            catch { Console.WriteLine(Program.GenerateSummary(text)); }
        });

        // summarize-state
        var sumStateCmd = new Command("summarize-state", "Summarize conversation and tasks");
        sumStateCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var text = string.Join("\n", state.Conversation) + "\n" + string.Join("\n", state.Tasks.Select(t => t.Description));
            if (string.IsNullOrWhiteSpace(text)) { Console.WriteLine("no data"); return; }
            try { Console.WriteLine(await SummarizeAsync(text)); }
            catch { Console.WriteLine(Program.GenerateSummary(text)); }
        });

        root.Add(convUniqueCmd);
        root.Add(dedupeMemCmd);
        root.Add(grepAdvCmd);
        root.Add(globAdvCmd);
        root.Add(rpcStreamCmd);
        root.Add(toolProgCmd);
        root.Add(toolProgAllCmd);
        root.Add(tasksTodayCmd);
        root.Add(tasksWeekCmd);
        root.Add(exportMdCmd);
        root.Add(sumFileCmd);
        root.Add(sumSecCmd);
        root.Add(convFreqCmd);
        root.Add(tasksTodayDueCmd);
        root.Add(sumTasksCmd);
        root.Add(sumStateCmd);
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

        // open-state
        var openStateCmd = new Command("open-state", "Open state.json in default editor");
        openStateCmd.SetHandler(async () =>
        {
            var psi = new ProcessStartInfo(Program.StatePath) { UseShellExecute = true };
            Process.Start(psi);
            await Task.CompletedTask;
        });

        root.Add(listCmd);
        root.Add(fileWritable);
        root.Add(dirWritable);
        root.Add(dirSize);
        root.Add(memoryStats);
        root.Add(memoryUnique);
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
        root.Add(tasksByPriorityCmd);
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
