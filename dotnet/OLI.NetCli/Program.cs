using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;

public class TaskRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "in-progress";
}

public class AppState
{
    public bool AgentMode { get; set; }
    public int SelectedModel { get; set; }
    public List<string> Conversation { get; set; } = new();
    public List<TaskRecord> Tasks { get; set; } = new();
    public HashSet<string> Subscriptions { get; set; } = new();
}

class Program
{
    static readonly string StatePath = Path.Combine(AppContext.BaseDirectory, "state.json");
    static readonly string MemoryPath = Path.Combine(AppContext.BaseDirectory, "oli.md");

    static AppState LoadState()
    {
        if (File.Exists(StatePath))
        {
            var json = File.ReadAllText(StatePath);
            return JsonSerializer.Deserialize<AppState>(json) ?? new AppState();
        }
        return new AppState();
    }

    static void SaveState(AppState state)
    {
        File.WriteAllText(StatePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
    }

    static int Main(string[] args)
    {
        var promptOption = new Option<string>("--prompt", "Prompt text") { IsRequired = true };
        var modelOption = new Option<int>("--model-index", () => 0, "Index of the model to use");
        var runCmd = new Command("run", "Send a prompt to the assistant")
        {
            promptOption,
            modelOption
        };
        runCmd.SetHandler(async (string prompt, int modelIndex) =>
        {
            var state = LoadState();
            state.SelectedModel = modelIndex;
            state.Conversation.Add($"User: {prompt}");
            SaveState(state);
            Console.WriteLine($"[Model {modelIndex}] Prompt: {prompt}");
            // TODO: call model API
            await Task.CompletedTask;
        }, promptOption, modelOption);

        var enableOption = new Option<bool>("--enable", "Set to true to enable agent mode") { IsRequired = true };
        var agentCmd = new Command("agent-mode", "Enable or disable agent mode")
        {
            enableOption
        };
        agentCmd.SetHandler(async (bool enable) =>
        {
            var state = LoadState();
            state.AgentMode = enable;
            SaveState(state);
            Console.WriteLine($"Agent mode set to {enable}");
            await Task.CompletedTask;
        }, enableOption);

        var agentStatusCmd = new Command("agent-status", "Show whether agent mode is enabled");
        agentStatusCmd.SetHandler(async () =>
        {
            var state = LoadState();
            Console.WriteLine(state.AgentMode ? "Agent mode is ON" : "Agent mode is OFF");
            await Task.CompletedTask;
        });

        var setModelCmd = new Command("set-model", "Select the active model")
        {
            modelOption
        };
        setModelCmd.SetHandler(async (int index) =>
        {
            var state = LoadState();
            state.SelectedModel = index;
            SaveState(state);
            Console.WriteLine($"Selected model {index}");
            await Task.CompletedTask;
        }, modelOption);

        var modelsCmd = new Command("models", "List available models");
        modelsCmd.SetHandler(async () =>
        {
            string[] models = ["gpt-4o", "claude-sonnet", "gemini-1.5" ];
            for (int i = 0; i < models.Length; i++)
            {
                Console.WriteLine($"[{i}] {models[i]}");
            }
            await Task.CompletedTask;
        });

        var tasksCmd = new Command("tasks", "List tasks");
        tasksCmd.SetHandler(async () =>
        {
            var state = LoadState();
            if (state.Tasks.Count == 0)
            {
                Console.WriteLine("No tasks.");
            }
            foreach (var t in state.Tasks)
            {
                Console.WriteLine($"{t.Id}: {t.Description} [{t.Status}]");
            }
            await Task.CompletedTask;
        });

        var descriptionOption = new Option<string>("--description") { IsRequired = true };
        var createTaskCmd = new Command("create-task", "Add a new task")
        {
            descriptionOption
        };
        createTaskCmd.SetHandler(async (string description) =>
        {
            var state = LoadState();
            var task = new TaskRecord { Description = description };
            state.Tasks.Add(task);
            SaveState(state);
            Console.WriteLine($"Created task {task.Id}");
            await Task.CompletedTask;
        }, descriptionOption);

        var completeIdOption = new Option<string>("--id") { IsRequired = true };
        var completeTaskCmd = new Command("complete-task", "Mark a task as completed")
        {
            completeIdOption
        };
        completeTaskCmd.SetHandler(async (string id) =>
        {
            var state = LoadState();
            var task = state.Tasks.Find(t => t.Id == id);
            if (task != null)
            {
                task.Status = "completed";
                SaveState(state);
                Console.WriteLine("Task completed");
            }
            else
            {
                Console.WriteLine("Task not found");
            }
            await Task.CompletedTask;
        }, completeIdOption);

        var cancelIdOption = new Option<string>("--id", () => string.Empty, "Task id to cancel");
        var cancelTaskCmd = new Command("cancel-task", "Cancel a task by id")
        {
            cancelIdOption
        };
        cancelTaskCmd.SetHandler(async (string id) =>
        {
            var state = LoadState();
            if (string.IsNullOrEmpty(id))
            {
                if (state.Tasks.Count > 0)
                {
                    state.Tasks[0].Status = "canceled";
                }
            }
            else
            {
                var task = state.Tasks.Find(t => t.Id == id);
                if (task != null) task.Status = "canceled";
            }
            SaveState(state);
            Console.WriteLine("Task canceled");
            await Task.CompletedTask;
        }, cancelIdOption);

        var clearConvCmd = new Command("clear-conversation", "Clear stored conversation");
        clearConvCmd.SetHandler(async () =>
        {
            var state = LoadState();
            state.Conversation.Clear();
            SaveState(state);
            Console.WriteLine("Conversation cleared");
            await Task.CompletedTask;
        });

        var conversationCmd = new Command("conversation", "Show conversation history");
        conversationCmd.SetHandler(async () =>
        {
            var state = LoadState();
            if (state.Conversation.Count == 0)
            {
                Console.WriteLine("No conversation yet.");
            }
            foreach (var m in state.Conversation)
            {
                Console.WriteLine(m);
            }
            await Task.CompletedTask;
        });

        var savePathOption = new Option<string>("--path") { IsRequired = true };
        var saveConvCmd = new Command("save-conversation", "Save conversation to file")
        {
            savePathOption
        };
        saveConvCmd.SetHandler(async (string path) =>
        {
            var state = LoadState();
            File.WriteAllLines(path, state.Conversation);
            Console.WriteLine($"Conversation saved to {path}");
            await Task.CompletedTask;
        }, savePathOption);

        var memoryInfoCmd = new Command("memory-info", "Show memory file path and content");
        memoryInfoCmd.SetHandler(async () =>
        {
            if (File.Exists(MemoryPath))
            {
                Console.WriteLine($"Memory path: {MemoryPath}");
                Console.WriteLine(File.ReadAllText(MemoryPath));
            }
            else
            {
                Console.WriteLine("No memory file found");
            }
            await Task.CompletedTask;
        });

        var sectionOption = new Option<string>("--section") { IsRequired = true };
        var memoryOption = new Option<string>("--memory") { IsRequired = true };
        var addMemoryCmd = new Command("add-memory", "Add a memory entry")
        {
            sectionOption,
            memoryOption
        };
        addMemoryCmd.SetHandler(async (string section, string memory) =>
        {
            var content = File.Exists(MemoryPath) ? File.ReadAllText(MemoryPath) : "";
            var sectionHeader = $"## {section}";
            if (!content.Contains(sectionHeader))
            {
                content += $"\n{sectionHeader}\n";
            }
            var insertIndex = content.IndexOf(sectionHeader) + sectionHeader.Length;
            content = content.Insert(insertIndex, $"\n- {memory}");
            File.WriteAllText(MemoryPath, content);
            Console.WriteLine("Memory added");
            await Task.CompletedTask;
        }, sectionOption, memoryOption);

        var contentOption = new Option<string>("--content") { IsRequired = true };
        var replaceMemoryCmd = new Command("replace-memory-file", "Replace entire memory file")
        {
            contentOption
        };
        replaceMemoryCmd.SetHandler(async (string content) =>
        {
            File.WriteAllText(MemoryPath, content);
            Console.WriteLine("Memory file replaced");
            await Task.CompletedTask;
        }, contentOption);

        var statePathCmd = new Command("state-path", "Show path of state file");
        statePathCmd.SetHandler(async () =>
        {
            Console.WriteLine(StatePath);
            await Task.CompletedTask;
        });

        var memoryPathCmd = new Command("memory-path", "Show path of memory file");
        memoryPathCmd.SetHandler(async () =>
        {
            Console.WriteLine(MemoryPath);
            await Task.CompletedTask;
        });

        var createMemoryCmd = new Command("create-memory-file", "Create memory file if missing");
        createMemoryCmd.SetHandler(async () =>
        {
            if (!File.Exists(MemoryPath))
            {
                var template = "# oli.md\n\n## Project Structure\n";
                File.WriteAllText(MemoryPath, template);
                Console.WriteLine("Memory file created");
            }
            else
            {
                Console.WriteLine("Memory file already exists");
            }
            await Task.CompletedTask;
        });

        var parseMemoryCmd = new Command("parsed-memory", "Show parsed memory sections");
        parseMemoryCmd.SetHandler(async () =>
        {
            if (!File.Exists(MemoryPath))
            {
                Console.WriteLine("No memory file found");
                return;
            }
            var lines = File.ReadAllLines(MemoryPath);
            string? current = null;
            var map = new Dictionary<string, List<string>>();
            foreach (var line in lines)
            {
                if (line.StartsWith("## "))
                {
                    current = line[3..];
                    map[current] = new List<string>();
                }
                else if (line.StartsWith("- ") && current != null)
                {
                    map[current].Add(line[2..]);
                }
            }
            foreach (var kv in map)
            {
                Console.WriteLine($"Section: {kv.Key}");
                foreach (var entry in kv.Value)
                {
                    Console.WriteLine($"  - {entry}");
                }
            }
            await Task.CompletedTask;
        });

        var summarizeCmd = new Command("summarize-conversation", "Summarize stored conversation");
        summarizeCmd.SetHandler(async () =>
        {
            var state = LoadState();
            if (state.Conversation.Count == 0)
            {
                Console.WriteLine("No conversation to summarize");
                return;
            }

            var text = string.Join(" ", state.Conversation);
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var summary = string.Join(" ", words.Take(20));
            state.Conversation.Clear();
            state.Conversation.Add($"[SUMMARY] {summary}...");
            SaveState(state);
            Console.WriteLine(summary);
            await Task.CompletedTask;
        });

        var convStatsCmd = new Command("conversation-stats", "Show conversation statistics");
        convStatsCmd.SetHandler(async () =>
        {
            var state = LoadState();
            var count = state.Conversation.Count;
            var chars = state.Conversation.Sum(m => m.Length);
            Console.WriteLine($"Messages: {count} Characters: {chars}");
            await Task.CompletedTask;
        });

        var readPathOption = new Option<string>("--path") { IsRequired = true };
        var readFileCmd = new Command("read-file", "Read file contents") { readPathOption };
        readFileCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return;
            }
            Console.WriteLine(File.ReadAllText(path));
            await Task.CompletedTask;
        }, readPathOption);

        var readNumberedCmd = new Command("read-file-numbered", "Read file with line numbers") { readPathOption };
        readNumberedCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return;
            }
            var lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                Console.WriteLine($"{i + 1,4} | {lines[i]}");
            }
            await Task.CompletedTask;
        }, readPathOption);

        var offsetOption = new Option<int>("--offset", () => 0);
        var limitOption = new Option<int?>("--limit", () => null);
        var readLinesCmd = new Command("read-file-lines", "Read a range of lines")
        {
            readPathOption, offsetOption, limitOption
        };
        readLinesCmd.SetHandler(async (string path, int offset, int? limit) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return;
            }
            var lines = File.ReadAllLines(path);
            var start = Math.Clamp(offset, 0, lines.Length);
            var end = limit.HasValue ? Math.Min(start + limit.Value, lines.Length) : lines.Length;
            for (int i = start; i < end; i++)
            {
                Console.WriteLine($"{i + 1,4} | {lines[i]}");
            }
            await Task.CompletedTask;
        }, readPathOption, offsetOption, limitOption);

        var contentOption2 = new Option<string>("--content") { IsRequired = true };
        var writeFileCmd = new Command("write-file", "Write content to a file") { readPathOption, contentOption2 };
        writeFileCmd.SetHandler(async (string path, string content) =>
        {
            File.WriteAllText(path, content);
            Console.WriteLine("File written");
            await Task.CompletedTask;
        }, readPathOption, contentOption2);

        var writeDiffCmd = new Command("write-file-diff", "Show diff then write file")
        {
            readPathOption, contentOption2
        };
        writeDiffCmd.SetHandler(async (string path, string content) =>
        {
            var old = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            var diff = GenerateDiff(old, content);
            File.WriteAllText(path, content);
            Console.WriteLine(diff);
            await Task.CompletedTask;
        }, readPathOption, contentOption2);

        var oldOpt = new Option<string>("--old") { IsRequired = true };
        var newOpt = new Option<string>("--new") { IsRequired = true };
        var editFileCmd = new Command("edit-file", "Replace text in a file")
        {
            readPathOption, oldOpt, newOpt
        };
        editFileCmd.SetHandler(async (string path, string oldStr, string newStr) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return;
            }
            var content = File.ReadAllText(path);
            var newContent = content.Replace(oldStr, newStr);
            var diff = GenerateDiff(content, newContent);
            File.WriteAllText(path, newContent);
            Console.WriteLine(diff);
            await Task.CompletedTask;
        }, readPathOption, oldOpt, newOpt);

        var dirPathOption = new Option<string>("--path", () => ".");
        var listDirCmd = new Command("list-directory", "List directory contents") { dirPathOption };
        listDirCmd.SetHandler(async (string path) =>
        {
            if (!Directory.Exists(path))
            {
                Console.WriteLine("Directory not found");
                return;
            }
            foreach (var entry in Directory.GetFileSystemEntries(path))
            {
                Console.WriteLine(entry);
            }
            await Task.CompletedTask;
        }, dirPathOption);

        var fileInfoCmd = new Command("file-info", "Show file metadata") { readPathOption };
        fileInfoCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                Console.WriteLine("Path not found");
                return;
            }
            var info = new FileInfo(path);
            Console.WriteLine($"Path: {info.FullName}\nSize: {info.Length} bytes\nModified: {info.LastWriteTime}");
            await Task.CompletedTask;
        }, readPathOption);

        var currentModelCmd = new Command("current-model", "Show selected model index");
        currentModelCmd.SetHandler(() =>
        {
            var state = LoadState();
            Console.WriteLine(state.SelectedModel);
            return Task.CompletedTask;
        });

        var listSubsCmd = new Command("subscriptions", "List active event subscriptions");
        listSubsCmd.SetHandler(() =>
        {
            var state = LoadState();
            if (state.Subscriptions.Count == 0)
            {
                Console.WriteLine("No subscriptions");
            }
            foreach (var s in state.Subscriptions)
            {
                Console.WriteLine(s);
            }
            return Task.CompletedTask;
        });

        var deleteSectionOption = new Option<string>("--section") { IsRequired = true };
        var deleteMemorySectionCmd = new Command("delete-memory-section", "Remove a memory section")
        {
            deleteSectionOption
        };
        deleteMemorySectionCmd.SetHandler(async (string section) =>
        {
            if (!File.Exists(MemoryPath))
            {
                Console.WriteLine("No memory file found");
                return;
            }
            var lines = File.ReadAllLines(MemoryPath);
            var result = new List<string>();
            bool skip = false;
            foreach (var line in lines)
            {
                if (line.StartsWith("## "))
                {
                    skip = line[3..] == section;
                    if (!skip) result.Add(line);
                    continue;
                }
                if (!skip) result.Add(line);
            }
            File.WriteAllLines(MemoryPath, result);
            Console.WriteLine($"Deleted section {section}");
            await Task.CompletedTask;
        }, deleteSectionOption);

        var deleteIdOption = new Option<string>("--id") { IsRequired = true };
        var deleteTaskCmd = new Command("delete-task", "Remove a task")
        {
            deleteIdOption
        };
        deleteTaskCmd.SetHandler(async (string id) =>
        {
            var state = LoadState();
            var idx = state.Tasks.FindIndex(t => t.Id == id);
            if (idx >= 0)
            {
                state.Tasks.RemoveAt(idx);
                SaveState(state);
                Console.WriteLine("Task removed");
            }
            else
            {
                Console.WriteLine("Task not found");
            }
            await Task.CompletedTask;
        }, deleteIdOption);

        var infoIdOption = new Option<string>("--id") { IsRequired = true };
        var taskInfoCmd = new Command("task-info", "Show details for a task")
        {
            infoIdOption
        };
        taskInfoCmd.SetHandler(async (string id) =>
        {
            var state = LoadState();
            var task = state.Tasks.Find(t => t.Id == id);
            if (task != null)
            {
                Console.WriteLine($"Id: {task.Id}\nDescription: {task.Description}\nStatus: {task.Status}");
            }
            else
            {
                Console.WriteLine("Task not found");
            }
            await Task.CompletedTask;
        }, infoIdOption);

        var resetStateCmd = new Command("reset-state", "Delete saved state file");
        resetStateCmd.SetHandler(async () =>
        {
            if (File.Exists(StatePath))
            {
                File.Delete(StatePath);
                Console.WriteLine("State reset");
            }
            else
            {
                Console.WriteLine("No state file");
            }
            await Task.CompletedTask;
        });

        var importPathOption = new Option<string>("--path") { IsRequired = true };
        var importStateCmd = new Command("import-state", "Load state from file")
        {
            importPathOption
        };
        importStateCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return;
            }
            var json = File.ReadAllText(path);
            var state = JsonSerializer.Deserialize<AppState>(json);
            if (state != null)
            {
                SaveState(state);
                Console.WriteLine("State imported");
            }
            else
            {
                Console.WriteLine("Invalid state file");
            }
            await Task.CompletedTask;
        }, importPathOption);

        var exportPathOption = new Option<string>("--path") { IsRequired = true };
        var exportStateCmd = new Command("export-state", "Write state to file")
        {
            exportPathOption
        };
        exportStateCmd.SetHandler(async (string path) =>
        {
            var state = LoadState();
            File.WriteAllText(path, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"State exported to {path}");
            await Task.CompletedTask;
        }, exportPathOption);

        var deleteMemoryFileCmd = new Command("delete-memory-file", "Remove memory file");
        deleteMemoryFileCmd.SetHandler(async () =>
        {
            if (File.Exists(MemoryPath))
            {
                File.Delete(MemoryPath);
                Console.WriteLine("Memory file deleted");
            }
            else
            {
                Console.WriteLine("No memory file found");
            }
            await Task.CompletedTask;
        });

        var listMemorySectionsCmd = new Command("list-memory-sections", "List memory sections");
        listMemorySectionsCmd.SetHandler(async () =>
        {
            if (!File.Exists(MemoryPath))
            {
                Console.WriteLine("No memory file found");
                return;
            }
            foreach (var line in File.ReadLines(MemoryPath))
            {
                if (line.StartsWith("## "))
                {
                    Console.WriteLine(line[3..]);
                }
            }
            await Task.CompletedTask;
        });

        var versionCmd = new Command("version", "Display CLI version");
        versionCmd.SetHandler(async () =>
        {
            var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";
            Console.WriteLine($"OLI.NetCli version {version}");
            await Task.CompletedTask;
        });

        var eventOption = new Option<string>("--event") { IsRequired = true };
        var subscribeCmd = new Command("subscribe", "Subscribe to an event type")
        {
            eventOption
        };
        subscribeCmd.SetHandler(async (string ev) =>
        {
            var state = LoadState();
            if (state.Subscriptions.Add(ev))
            {
                SaveState(state);
                Console.WriteLine($"Subscribed to {ev}");
            }
            else
            {
                Console.WriteLine($"Already subscribed to {ev}");
            }
            await Task.CompletedTask;
        }, eventOption);

        var unsubscribeCmd = new Command("unsubscribe", "Unsubscribe from an event type")
        {
            eventOption
        };
        unsubscribeCmd.SetHandler(async (string ev) =>
        {
            var state = LoadState();
            if (state.Subscriptions.Remove(ev))
            {
                SaveState(state);
                Console.WriteLine($"Unsubscribed from {ev}");
            }
            else
            {
                Console.WriteLine($"Not subscribed to {ev}");
            }
            await Task.CompletedTask;
        }, eventOption);

        var root = new RootCommand("oli .NET CLI")
        {
            runCmd, agentCmd, agentStatusCmd, setModelCmd, modelsCmd,
            tasksCmd, createTaskCmd, completeTaskCmd, cancelTaskCmd,
            clearConvCmd, conversationCmd, saveConvCmd,
            memoryInfoCmd, memoryPathCmd, createMemoryCmd,
            addMemoryCmd, replaceMemoryCmd, parseMemoryCmd,
            summarizeCmd, convStatsCmd,
            readFileCmd, readNumberedCmd, readLinesCmd,
            writeFileCmd, writeDiffCmd, editFileCmd,
            listDirCmd, fileInfoCmd,
            currentModelCmd, listSubsCmd, deleteMemorySectionCmd,
            deleteTaskCmd, taskInfoCmd, resetStateCmd,
            importStateCmd, exportStateCmd, deleteMemoryFileCmd,
            listMemorySectionsCmd, statePathCmd, versionCmd,
            subscribeCmd, unsubscribeCmd
        };

        return root.Invoke(args);
    }

    static string GenerateDiff(string oldContent, string newContent)
    {
        var oldLines = oldContent.Split('\n');
        var newLines = newContent.Split('\n');
        var max = Math.Max(oldLines.Length, newLines.Length);
        var diff = new List<string>();
        for (int i = 0; i < max; i++)
        {
            var oldLine = i < oldLines.Length ? oldLines[i] : string.Empty;
            var newLine = i < newLines.Length ? newLines[i] : string.Empty;
            if (oldLine != newLine)
            {
                if (!string.IsNullOrEmpty(oldLine)) diff.Add($"- {oldLine}");
                if (!string.IsNullOrEmpty(newLine)) diff.Add($"+ {newLine}");
            }
        }
        return string.Join('\n', diff);
    }
}
