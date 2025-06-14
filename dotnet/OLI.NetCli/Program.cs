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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int ToolCount { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}

public class AppState
{
    public bool AgentMode { get; set; }
    public int SelectedModel { get; set; }
    public List<string> Conversation { get; set; } = new();
    public List<ConversationSummary> ConversationSummaries { get; set; } = new();
    public List<TaskRecord> Tasks { get; set; } = new();
    public HashSet<string> Subscriptions { get; set; } = new();
}

class Program
{
    static readonly string StatePath = Path.Combine(AppContext.BaseDirectory, "state.json");
    static readonly string TasksPath = Path.Combine(AppContext.BaseDirectory, "tasks.json");
    static readonly string ConversationPath = Path.Combine(AppContext.BaseDirectory, "conversation.json");
    static readonly string SummariesPath = Path.Combine(AppContext.BaseDirectory, "summaries.json");
    static readonly string MemoryPath = Path.Combine(AppContext.BaseDirectory, "oli.md");

    static List<TaskRecord> LoadTasks()
    {
        if (File.Exists(TasksPath))
        {
            var json = File.ReadAllText(TasksPath);
            return JsonSerializer.Deserialize<List<TaskRecord>>(json) ?? new();
        }
        return new();
    }

    static List<ConversationSummary> LoadSummaries()
    {
        if (File.Exists(SummariesPath))
        {
            var json = File.ReadAllText(SummariesPath);
            return JsonSerializer.Deserialize<List<ConversationSummary>>(json) ?? new();
        }
        return new();
    }

    static void SaveTasks(List<TaskRecord> tasks)
    {
        File.WriteAllText(TasksPath, JsonSerializer.Serialize(tasks, new JsonSerializerOptions { WriteIndented = true }));
    }

    static List<string> LoadConversation()
    {
        return File.Exists(ConversationPath) ? File.ReadAllLines(ConversationPath).ToList() : new();
    }

    static void SaveConversation(List<string> conv)
    {
        File.WriteAllLines(ConversationPath, conv);
    }

    static void SaveSummaries(List<ConversationSummary> summaries)
    {
        File.WriteAllText(SummariesPath, JsonSerializer.Serialize(summaries, new JsonSerializerOptions { WriteIndented = true }));
    }

    static AppState LoadState()
    {
        var state = File.Exists(StatePath)
            ? JsonSerializer.Deserialize<AppState>(File.ReadAllText(StatePath)) ?? new AppState()
            : new AppState();

        state.Tasks = LoadTasks();
        state.Conversation = LoadConversation();
        state.ConversationSummaries = LoadSummaries();
        return state;
    }

    static void SaveState(AppState state)
    {
        File.WriteAllText(StatePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        SaveTasks(state.Tasks);
        SaveConversation(state.Conversation);
        SaveSummaries(state.ConversationSummaries);
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
                var duration = (t.UpdatedAt - t.CreatedAt).TotalSeconds;
                Console.WriteLine($"{t.Id}: {t.Description} [{t.Status}] Tools:{t.ToolCount} Tokens:{t.InputTokens}/{t.OutputTokens} Duration:{duration:F0}s");
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
            var task = new TaskRecord
            {
                Description = description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            state.Tasks.Add(task);
            SaveState(state);
            Console.WriteLine($"Created task {task.Id}");
            await Task.CompletedTask;
        }, descriptionOption);

        var completeIdOption = new Option<string>("--id") { IsRequired = true };
        var outputTokensOption = new Option<int>("--output-tokens", () => 0);
        var completeTaskCmd = new Command("complete-task", "Mark a task as completed")
        {
            completeIdOption, outputTokensOption
        };
        completeTaskCmd.SetHandler(async (string id, int outputTokens) =>
        {
            var state = LoadState();
            var task = state.Tasks.Find(t => t.Id == id);
            if (task != null)
            {
                task.Status = "completed";
                task.OutputTokens = outputTokens;
                task.UpdatedAt = DateTime.UtcNow;
                SaveState(state);
                Console.WriteLine("Task completed");
            }
            else
            {
                Console.WriteLine("Task not found");
            }
            await Task.CompletedTask;
        }, completeIdOption, outputTokensOption);

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

        var statsIdOpt = new Option<string>("--id") { IsRequired = true };
        var taskStatsCmd = new Command("task-stats", "Show task details")
        {
            statsIdOpt
        };
        taskStatsCmd.SetHandler(async (string id) =>
        {
            var state = LoadState();
            var task = state.Tasks.Find(t => t.Id == id);
            if (task != null)
            {
                var duration = (task.UpdatedAt - task.CreatedAt).TotalSeconds;
                Console.WriteLine($"Id: {task.Id}\nDesc: {task.Description}\nStatus: {task.Status}\nTools: {task.ToolCount}\nInput Tokens: {task.InputTokens}\nOutput Tokens: {task.OutputTokens}\nDuration: {duration:F0}s");
            }
            else
            {
                Console.WriteLine("Task not found");
            }
            await Task.CompletedTask;
        }, statsIdOpt);

        var addTokensIdOpt = new Option<string>("--id") { IsRequired = true };
        var tokensOpt = new Option<int>("--tokens") { IsRequired = true };
        var addInputTokensCmd = new Command("add-input-tokens", "Add tokens to a task")
        {
            addTokensIdOpt, tokensOpt
        };
        addInputTokensCmd.SetHandler(async (string id, int tokens) =>
        {
            var state = LoadState();
            var task = state.Tasks.Find(t => t.Id == id);
            if (task != null)
            {
                task.InputTokens += tokens;
                task.UpdatedAt = DateTime.UtcNow;
                SaveState(state);
                Console.WriteLine($"Added {tokens} tokens");
            }
            else
            {
                Console.WriteLine("Task not found");
            }
            await Task.CompletedTask;
        }, addTokensIdOpt, tokensOpt);

        var toolIdOpt = new Option<string>("--id") { IsRequired = true };
        var addToolUseCmd = new Command("add-tool-use", "Increment tool count")
        {
            toolIdOpt
        };
        addToolUseCmd.SetHandler(async (string id) =>
        {
            var state = LoadState();
            var task = state.Tasks.Find(t => t.Id == id);
            if (task != null)
            {
                task.ToolCount += 1;
                task.UpdatedAt = DateTime.UtcNow;
                SaveState(state);
                Console.WriteLine("Tool count updated");
            }
            else
            {
                Console.WriteLine("Task not found");
            }
            await Task.CompletedTask;
        }, toolIdOpt);

        var taskCountCmd = new Command("task-count", "Show number of tasks");
        taskCountCmd.SetHandler(async () =>
        {
            var state = LoadState();
            Console.WriteLine(state.Tasks.Count);
            await Task.CompletedTask;
        });

        var clearTasksCmd = new Command("clear-tasks", "Remove all tasks");
        clearTasksCmd.SetHandler(async () =>
        {
            var state = LoadState();
            state.Tasks.Clear();
            SaveState(state);
            Console.WriteLine("Tasks cleared");
            await Task.CompletedTask;
        });

        var clearCompletedCmd = new Command("clear-completed-tasks", "Remove completed tasks");
        clearCompletedCmd.SetHandler(async () =>
        {
            var state = LoadState();
            state.Tasks.RemoveAll(t => t.Status == "completed");
            SaveState(state);
            Console.WriteLine("Completed tasks removed");
            await Task.CompletedTask;
        });

        var statusOpt = new Option<string>("--status") { IsRequired = true };
        var tasksByStatusCmd = new Command("tasks-by-status", "List tasks filtered by status") { statusOpt };
        tasksByStatusCmd.SetHandler(async (string status) =>
        {
            var state = LoadState();
            foreach (var t in state.Tasks.Where(t => string.Equals(t.Status, status, StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"{t.Id}: {t.Description} [{t.Status}]");
            }
            await Task.CompletedTask;
        }, statusOpt);

        var updateDescIdOpt = new Option<string>("--id") { IsRequired = true };
        var updateDescOpt = new Option<string>("--description") { IsRequired = true };
        var updateTaskDescCmd = new Command("update-task-desc", "Update task description")
        {
            updateDescIdOpt, updateDescOpt
        };
        updateTaskDescCmd.SetHandler(async (string id, string description) =>
        {
            var state = LoadState();
            var task = state.Tasks.Find(t => t.Id == id);
            if (task != null)
            {
                task.Description = description;
                task.UpdatedAt = DateTime.UtcNow;
                SaveState(state);
                Console.WriteLine("Description updated");
            }
            else
            {
                Console.WriteLine("Task not found");
            }
            await Task.CompletedTask;
        }, updateDescIdOpt, updateDescOpt);

        var exportTasksPathOpt = new Option<string>("--path") { IsRequired = true };
        var exportTasksCmd = new Command("export-tasks", "Save tasks to JSON file")
        {
            exportTasksPathOpt
        };
        exportTasksCmd.SetHandler(async (string path) =>
        {
            var state = LoadState();
            File.WriteAllText(path, JsonSerializer.Serialize(state.Tasks, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Tasks exported to {path}");
            await Task.CompletedTask;
        }, exportTasksPathOpt);

        var importTasksPathOpt = new Option<string>("--path") { IsRequired = true };
        var importTasksCmd = new Command("import-tasks", "Load tasks from JSON file")
        {
            importTasksPathOpt
        };
        importTasksCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return;
            }
            var json = File.ReadAllText(path);
            var tasks = JsonSerializer.Deserialize<List<TaskRecord>>(json);
            if (tasks != null)
            {
                var state = LoadState();
                state.Tasks = tasks;
                SaveState(state);
                Console.WriteLine("Tasks imported");
            }
            else
            {
                Console.WriteLine("Invalid tasks file");
            }
            await Task.CompletedTask;
        }, importTasksPathOpt);

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

        var exportConvPathOpt = new Option<string>("--path") { IsRequired = true };
        var exportConvCmd = new Command("export-conversation", "Export conversation to file")
        {
            exportConvPathOpt
        };
        exportConvCmd.SetHandler(async (string path) =>
        {
            var state = LoadState();
            File.WriteAllLines(path, state.Conversation);
            Console.WriteLine($"Conversation exported to {path}");
            await Task.CompletedTask;
        }, exportConvPathOpt);

        var importConvPathOpt = new Option<string>("--path") { IsRequired = true };
        var importConvCmd = new Command("import-conversation", "Load conversation from file")
        {
            importConvPathOpt
        };
        importConvCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return;
            }
            var lines = File.ReadAllLines(path).ToList();
            var state = LoadState();
            state.Conversation = lines;
            SaveState(state);
            Console.WriteLine("Conversation loaded");
            await Task.CompletedTask;
        }, importConvPathOpt);

        var appendConvPathOpt = new Option<string>("--path") { IsRequired = true };
        var appendConvCmd = new Command("append-conversation", "Append messages from file to conversation")
        {
            appendConvPathOpt
        };
        appendConvCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return;
            }
            var lines = File.ReadAllLines(path);
            var state = LoadState();
            state.Conversation.AddRange(lines);
            SaveState(state);
            Console.WriteLine("Conversation updated");
            await Task.CompletedTask;
        }, appendConvPathOpt);

        var deleteIndexOpt = new Option<int>("--index") { IsRequired = true };
        var deleteConvMsgCmd = new Command("delete-conversation-message", "Remove a message by index")
        {
            deleteIndexOpt
        };
        deleteConvMsgCmd.SetHandler(async (int index) =>
        {
            var state = LoadState();
            if (index >= 0 && index < state.Conversation.Count)
            {
                state.Conversation.RemoveAt(index);
                SaveState(state);
                Console.WriteLine("Message removed");
            }
            else
            {
                Console.WriteLine("Invalid index");
            }
            await Task.CompletedTask;
        }, deleteIndexOpt);

        var convLenCmd = new Command("conversation-length", "Show number of conversation messages");
        convLenCmd.SetHandler(async () =>
        {
            var state = LoadState();
            Console.WriteLine(state.Conversation.Count);
            await Task.CompletedTask;
        });

        var lastConvCmd = new Command("conversation-last", "Show last conversation message");
        lastConvCmd.SetHandler(async () =>
        {
            var state = LoadState();
            if (state.Conversation.Count == 0)
            {
                Console.WriteLine("No conversation");
            }
            else
            {
                Console.WriteLine(state.Conversation.Last());
            }
            await Task.CompletedTask;
        });

        var searchTextOpt = new Option<string>("--text") { IsRequired = true };
        var convSearchCmd = new Command("conversation-search", "Search conversation for text") { searchTextOpt };
        convSearchCmd.SetHandler(async (string text) =>
        {
            var state = LoadState();
            var matches = state.Conversation
                .Select((m, i) => (m, i))
                .Where(t => t.m.Contains(text, StringComparison.OrdinalIgnoreCase));
            foreach (var (m, i) in matches)
            {
                Console.WriteLine($"[{i}] {m}");
            }
            await Task.CompletedTask;
        }, searchTextOpt);

        var startOpt = new Option<int>("--start") { IsRequired = true };
        var endOpt = new Option<int>("--end") { IsRequired = true };
        var deleteRangeCmd = new Command("delete-conversation-range", "Delete messages in index range") { startOpt, endOpt };
        deleteRangeCmd.SetHandler(async (int start, int end) =>
        {
            var state = LoadState();
            if (start < 0 || end >= state.Conversation.Count || start > end)
            {
                Console.WriteLine("Invalid range");
                return;
            }
            state.Conversation.RemoveRange(start, end - start + 1);
            SaveState(state);
            Console.WriteLine("Messages removed");
            await Task.CompletedTask;
        }, startOpt, endOpt);

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

        var appendContentOpt = new Option<string>("--content") { IsRequired = true };
        var appendMemoryCmd = new Command("append-memory-file", "Append text to memory file")
        {
            appendContentOpt
        };
        appendMemoryCmd.SetHandler(async (string content) =>
        {
            var existing = File.Exists(MemoryPath) ? File.ReadAllText(MemoryPath) : string.Empty;
            File.WriteAllText(MemoryPath, existing + content);
            Console.WriteLine("Memory file updated");
            await Task.CompletedTask;
        }, appendContentOpt);

        var importMemoryPathOpt = new Option<string>("--path") { IsRequired = true };
        var importMemoryCmd = new Command("import-memory-file", "Load memory file from path")
        {
            importMemoryPathOpt
        };
        importMemoryCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return;
            }
            File.Copy(path, MemoryPath, true);
            Console.WriteLine("Memory file imported");
            await Task.CompletedTask;
        }, importMemoryPathOpt);

        var exportMemoryPathOpt = new Option<string>("--path") { IsRequired = true };
        var exportMemoryCmd = new Command("export-memory-file", "Write memory file to path")
        {
            exportMemoryPathOpt
        };
        exportMemoryCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(MemoryPath))
            {
                Console.WriteLine("No memory file found");
                return;
            }
            File.Copy(MemoryPath, path, true);
            Console.WriteLine($"Memory file exported to {path}");
            await Task.CompletedTask;
        }, exportMemoryPathOpt);

        var statePathCmd = new Command("state-path", "Show path of state file");
        statePathCmd.SetHandler(async () =>
        {
            Console.WriteLine(StatePath);
            await Task.CompletedTask;
        });

        var stateInfoCmd = new Command("state-info", "Show summary of saved state");
        stateInfoCmd.SetHandler(async () =>
        {
            var state = LoadState();
            Console.WriteLine($"AgentMode:{state.AgentMode} Model:{state.SelectedModel} Tasks:{state.Tasks.Count} Messages:{state.Conversation.Count}");
            await Task.CompletedTask;
        });

        var memoryPathCmd = new Command("memory-path", "Show path of memory file");
        memoryPathCmd.SetHandler(async () =>
        {
            Console.WriteLine(MemoryPath);
            await Task.CompletedTask;
        });

        var memoryExistsCmd = new Command("memory-exists", "Check for memory file");
        memoryExistsCmd.SetHandler(async () =>
        {
            Console.WriteLine(File.Exists(MemoryPath) ? "true" : "false");
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

        var sectionCountCmd = new Command("memory-section-count", "Count memory sections");
        sectionCountCmd.SetHandler(async () =>
        {
            if (!File.Exists(MemoryPath))
            {
                Console.WriteLine("0");
                return;
            }
            var count = File.ReadLines(MemoryPath).Count(l => l.StartsWith("## "));
            Console.WriteLine(count);
            await Task.CompletedTask;
        });

        var entryCountCmd = new Command("memory-entry-count", "Count total memory entries");
        entryCountCmd.SetHandler(async () =>
        {
            if (!File.Exists(MemoryPath))
            {
                Console.WriteLine("0");
                return;
            }
            var count = File.ReadLines(MemoryPath).Count(l => l.StartsWith("- "));
            Console.WriteLine(count);
            await Task.CompletedTask;
        });

        var memoryTemplateCmd = new Command("memory-template", "Show default memory template");
        memoryTemplateCmd.SetHandler(async () =>
        {
            Console.WriteLine("# oli.md\n\n## Project Structure\n- example\n\n## Build Commands\n- example\n\n## Test Commands\n- example\n\n## Architecture\n- example");
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

        var convCharCountCmd = new Command("conversation-char-count", "Show total character count of conversation");
        convCharCountCmd.SetHandler(async () =>
        {
            var state = LoadState();
            var chars = state.Conversation.Sum(m => m.Length);
            Console.WriteLine(chars);
            await Task.CompletedTask;
        });

        var summaryCountCmd = new Command("summary-count", "Show number of conversation summaries");
        summaryCountCmd.SetHandler(async () =>
        {
            var state = LoadState();
            Console.WriteLine(state.ConversationSummaries.Count);
            await Task.CompletedTask;
        });

        var compressConvCmd = new Command("compress-conversation", "Summarize and clear conversation");
        compressConvCmd.SetHandler(async () =>
        {
            var state = LoadState();
            if (state.Conversation.Count == 0)
            {
                Console.WriteLine("No conversation to compress");
                return;
            }
            var text = string.Join(" ", state.Conversation);
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var summary = string.Join(" ", words.Take(20));
            state.ConversationSummaries.Add(new ConversationSummary
            {
                Content = summary,
                CreatedAt = DateTime.UtcNow,
                MessagesCount = state.Conversation.Count,
                OriginalChars = text.Length
            });
            state.Conversation.Clear();
            SaveState(state);
            Console.WriteLine(summary);
            await Task.CompletedTask;
        });

        var clearHistoryCmd = new Command("clear-history", "Remove conversation and summaries");
        clearHistoryCmd.SetHandler(async () =>
        {
            var state = LoadState();
            state.Conversation.Clear();
            state.ConversationSummaries.Clear();
            SaveState(state);
            Console.WriteLine("History cleared");
            await Task.CompletedTask;
        });

        var showSummariesCmd = new Command("show-summaries", "Display conversation summaries");
        showSummariesCmd.SetHandler(async () =>
        {
            var state = LoadState();
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
            var state = LoadState();
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
            var summaries = JsonSerializer.Deserialize<List<ConversationSummary>>(json);
            if (summaries != null)
            {
                var state = LoadState();
                state.ConversationSummaries = summaries;
                SaveState(state);
                Console.WriteLine("Summaries imported");
            }
            else
            {
                Console.WriteLine("Invalid summaries file");
            }
            await Task.CompletedTask;
        }, importSummariesPathOpt);

        var deleteSummaryIndexOpt = new Option<int>("--index") { IsRequired = true };
        var deleteSummaryCmd = new Command("delete-summary", "Remove a summary by index") { deleteSummaryIndexOpt };
        deleteSummaryCmd.SetHandler(async (int index) =>
        {
            var state = LoadState();
            if (index >= 0 && index < state.ConversationSummaries.Count)
            {
                state.ConversationSummaries.RemoveAt(index);
                SaveState(state);
                Console.WriteLine("Summary deleted");
            }
            else
            {
                Console.WriteLine("Invalid index");
            }
            await Task.CompletedTask;
        }, deleteSummaryIndexOpt);

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

        var genWriteDiffCmd = new Command("generate-write-diff", "Preview diff without writing")
        {
            readPathOption, contentOption2
        };
        genWriteDiffCmd.SetHandler(async (string path, string content) =>
        {
            var old = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            var diff = GenerateDiff(old, content);
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

        var genEditDiffCmd = new Command("generate-edit-diff", "Preview edit diff")
        {
            readPathOption, oldOpt, newOpt
        };
        genEditDiffCmd.SetHandler(async (string path, string oldStr, string newStr) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return;
            }
            var content = File.ReadAllText(path);
            var newContent = content.Replace(oldStr, newStr);
            var diff = GenerateDiff(content, newContent);
            Console.WriteLine(diff);
            await Task.CompletedTask;
        }, readPathOption, oldOpt, newOpt);

        var appendContentOption = new Option<string>("--content") { IsRequired = true };
        var appendFileCmd = new Command("append-file", "Append content to a file")
        {
            readPathOption, appendContentOption
        };
        appendFileCmd.SetHandler(async (string path, string content) =>
        {
            await File.AppendAllTextAsync(path, content);
            Console.WriteLine("File appended");
        }, readPathOption, appendContentOption);

        var copyDestOption = new Option<string>("--dest") { IsRequired = true };
        var copyFileCmd = new Command("copy-file", "Copy file to destination")
        {
            readPathOption, copyDestOption
        };
        copyFileCmd.SetHandler(async (string path, string dest) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return;
            }
            File.Copy(path, dest, true);
            Console.WriteLine($"Copied to {dest}");
            await Task.CompletedTask;
        }, readPathOption, copyDestOption);

        var moveDestOption = new Option<string>("--dest") { IsRequired = true };
        var moveFileCmd = new Command("move-file", "Move file to destination")
        {
            readPathOption, moveDestOption
        };
        moveFileCmd.SetHandler(async (string path, string dest) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return;
            }
            File.Move(path, dest, true);
            Console.WriteLine($"Moved to {dest}");
            await Task.CompletedTask;
        }, readPathOption, moveDestOption);

        var renameDestOption = new Option<string>("--new-path") { IsRequired = true };
        var renameFileCmd = new Command("rename-file", "Rename a file")
        {
            readPathOption, renameDestOption
        };
        renameFileCmd.SetHandler(async (string path, string newPath) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return;
            }
            File.Move(path, newPath, true);
            Console.WriteLine($"Renamed to {newPath}");
            await Task.CompletedTask;
        }, readPathOption, renameDestOption);

        var deleteFileCmd = new Command("delete-file", "Delete a file") { readPathOption };
        deleteFileCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return;
            }
            File.Delete(path);
            Console.WriteLine("File deleted");
            await Task.CompletedTask;
        }, readPathOption);

        var fileExistsCmd = new Command("file-exists", "Check if a file exists") { readPathOption };
        fileExistsCmd.SetHandler(async (string path) =>
        {
            Console.WriteLine(File.Exists(path) ? "true" : "false");
            await Task.CompletedTask;
        }, readPathOption);

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

        var createDirCmd = new Command("create-directory", "Create a directory") { dirPathOption };
        createDirCmd.SetHandler(async (string path) =>
        {
            Directory.CreateDirectory(path);
            Console.WriteLine($"Created {path}");
            await Task.CompletedTask;
        }, dirPathOption);

        var deleteDirCmd = new Command("delete-directory", "Delete a directory") { dirPathOption };
        deleteDirCmd.SetHandler(async (string path) =>
        {
            if (!Directory.Exists(path))
            {
                Console.WriteLine("Directory not found");
                return;
            }
            Directory.Delete(path, true);
            Console.WriteLine("Directory deleted");
            await Task.CompletedTask;
        }, dirPathOption);

        var dirExistsCmd = new Command("dir-exists", "Check if directory exists") { dirPathOption };
        dirExistsCmd.SetHandler(async (string path) =>
        {
            Console.WriteLine(Directory.Exists(path) ? "true" : "false");
            await Task.CompletedTask;
        }, dirPathOption);

        var globPatternOpt = new Option<string>("--pattern") { IsRequired = true };
        var globSearchCmd = new Command("glob-search", "Search files by glob pattern") { globPatternOpt };
        globSearchCmd.SetHandler(async (string pattern) =>
        {
            var files = Directory.GetFiles(".", pattern, SearchOption.AllDirectories);
            foreach (var f in files) Console.WriteLine(f);
            await Task.CompletedTask;
        }, globPatternOpt);

        var globDirOpt = new Option<string>("--dir") { IsRequired = true };
        var globSearchInDirCmd = new Command("glob-search-in-dir", "Glob search in directory") { globDirOpt, globPatternOpt };
        globSearchInDirCmd.SetHandler(async (string dir, string pattern) =>
        {
            if (!Directory.Exists(dir))
            {
                Console.WriteLine("Directory not found");
                return;
            }
            var files = Directory.GetFiles(dir, pattern, SearchOption.AllDirectories);
            foreach (var f in files) Console.WriteLine(f);
            await Task.CompletedTask;
        }, globDirOpt, globPatternOpt);

        var grepPatternOpt = new Option<string>("--pattern") { IsRequired = true };
        var grepDirOpt = new Option<string>("--dir", () => ".");
        var grepSearchCmd = new Command("grep-search", "Regex search in files") { grepPatternOpt, grepDirOpt };
        grepSearchCmd.SetHandler(async (string pattern, string dir) =>
        {
            if (!Directory.Exists(dir))
            {
                Console.WriteLine("Directory not found");
                return;
            }
            var regex = new System.Text.RegularExpressions.Regex(pattern);
            foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                foreach (var (line, index) in File.ReadLines(file).Select((l, i) => (l, i + 1)))
                {
                    if (regex.IsMatch(line))
                    {
                        Console.WriteLine($"{file}:{index}:{line}");
                    }
                }
            }
            await Task.CompletedTask;
        }, grepPatternOpt, grepDirOpt);

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

        var countLinesCmd = new Command("count-lines", "Count lines in a file") { readPathOption };
        countLinesCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return;
            }
            var lines = await File.ReadAllLinesAsync(path);
            Console.WriteLine(lines.Length);
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
            sectionCountCmd, entryCountCmd, memoryTemplateCmd,
            summarizeCmd, convStatsCmd,
            convCharCountCmd, summaryCountCmd, compressConvCmd,
            clearHistoryCmd, showSummariesCmd, exportSummariesCmd,
            importSummariesCmd, deleteSummaryCmd,
            readFileCmd, readNumberedCmd, readLinesCmd,
            writeFileCmd, writeDiffCmd, editFileCmd, appendFileCmd,
            genWriteDiffCmd, genEditDiffCmd, copyFileCmd, moveFileCmd, renameFileCmd,
            deleteFileCmd, fileExistsCmd, listDirCmd, createDirCmd, deleteDirCmd, dirExistsCmd, fileInfoCmd, countLinesCmd,
            globSearchCmd, globSearchInDirCmd, grepSearchCmd,
            currentModelCmd, listSubsCmd, deleteMemorySectionCmd,
            deleteTaskCmd, taskInfoCmd, taskStatsCmd,
            addInputTokensCmd, addToolUseCmd, resetStateCmd,
            importStateCmd, exportStateCmd, deleteMemoryFileCmd,
            listMemorySectionsCmd, appendMemoryCmd, importMemoryCmd, exportMemoryCmd, statePathCmd, stateInfoCmd, versionCmd,
            memoryExistsCmd, subscribeCmd, unsubscribeCmd,
            taskCountCmd, clearTasksCmd, clearCompletedCmd, tasksByStatusCmd, updateTaskDescCmd, exportTasksCmd,
            importTasksCmd, importConvCmd, appendConvCmd, convLenCmd, lastConvCmd, convSearchCmd, deleteRangeCmd, exportConvCmd, deleteConvMsgCmd
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
