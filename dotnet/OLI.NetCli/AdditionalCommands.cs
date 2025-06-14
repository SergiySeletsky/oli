using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Net.Http;
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
    }
}
