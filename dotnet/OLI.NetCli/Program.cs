using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

// Local utilities
using static FileUtils;
using static JsonUtils;
using static MemoryUtils;
using static LogUtils;
using static KernelUtils;
using static HistoryCommands;

class Program
{
    public static readonly string StatePath = Path.Combine(AppContext.BaseDirectory, "state.json");
    public static readonly string TasksPath = Path.Combine(AppContext.BaseDirectory, "tasks.json");
    public static readonly string ConversationPath = Path.Combine(AppContext.BaseDirectory, "conversation.json");
    public static readonly string SummariesPath = Path.Combine(AppContext.BaseDirectory, "summaries.json");
    public static readonly string HistoryPath = Path.Combine(AppContext.BaseDirectory, "history.jsonl");
    public static readonly string ToolsPath = Path.Combine(AppContext.BaseDirectory, "tools.json");
    public static readonly string MemoryPath = Path.Combine(AppContext.BaseDirectory, "oli.md");
    public static readonly string LspPath = Path.Combine(AppContext.BaseDirectory, "lsp.json");

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

    static List<ToolExecution> LoadTools()
    {
        if (File.Exists(ToolsPath))
        {
            var json = File.ReadAllText(ToolsPath);
            return JsonSerializer.Deserialize<List<ToolExecution>>(json) ?? new();
        }
        return new();
    }

    static void SaveTasks(List<TaskRecord> tasks)
    {
        File.WriteAllText(TasksPath, JsonSerializer.Serialize(tasks, new JsonSerializerOptions { WriteIndented = true }));
    }

    static void SaveTools(List<ToolExecution> tools)
    {
        File.WriteAllText(ToolsPath, JsonSerializer.Serialize(tools, new JsonSerializerOptions { WriteIndented = true }));
    }

    static List<LspServerInfo> LoadLspServers()
    {
        if (File.Exists(LspPath))
        {
            var json = File.ReadAllText(LspPath);
            return JsonSerializer.Deserialize<List<LspServerInfo>>(json) ?? new();
        }
        return new();
    }

    static void SaveLspServers(List<LspServerInfo> servers)
    {
        File.WriteAllText(LspPath, JsonSerializer.Serialize(servers, new JsonSerializerOptions { WriteIndented = true }));
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

    public static AppState LoadState()
    {
        var state = File.Exists(StatePath)
            ? JsonSerializer.Deserialize<AppState>(File.ReadAllText(StatePath)) ?? new AppState()
            : new AppState();

        state.Tasks = LoadTasks();
        state.Conversation = LoadConversation();
        state.ConversationSummaries = LoadSummaries();
        state.ToolExecutions = LoadTools();
        state.LspServers = LoadLspServers();
        return state;
    }

    public static void SaveState(AppState state)
    {
        var oldConv = LoadConversation();
        File.WriteAllText(StatePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        SaveTasks(state.Tasks);
        SaveConversation(state.Conversation);
        SaveSummaries(state.ConversationSummaries);
        SaveTools(state.ToolExecutions);
        SaveLspServers(state.LspServers);
        if (state.Conversation.Count > oldConv.Count)
            HistoryCommands.AppendHistory(state.Conversation.Skip(oldConv.Count));
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
            await AutoCompress(state);
            SaveState(state);
            Console.WriteLine($"[Model {modelIndex}] Prompt: {prompt}");
            try
            {
                var history = string.Join("\n", state.Conversation);
                var fullPrompt = string.IsNullOrWhiteSpace(history)
                    ? prompt
                    : $"{history}\nUser: {prompt}\nAssistant:";
                var reply = await CompleteAsync(fullPrompt);
                state.Conversation.Add($"Assistant: {reply}");
                SaveState(state);
                Console.WriteLine(reply);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
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
                var due = t.DueDate != null ? $" Due:{t.DueDate:u}" : "";
                var tags = t.Tags.Count > 0 ? $" Tags:{string.Join(',', t.Tags)}" : "";
                Console.WriteLine($"{t.Id}: {t.Description} [{t.Status}] Tools:{t.ToolCount} Tokens:{t.InputTokens}/{t.OutputTokens} Duration:{duration:F0}s{due}{tags}");
            }
            await Task.CompletedTask;
        });

        var descriptionOption = new Option<string>("--description") { IsRequired = true };
        var dueOption = new Option<string?>("--due");
        var tagsOption = new Option<string?>("--tags", "comma separated tags");
        var createTaskCmd = new Command("create-task", "Add a new task")
        {
            descriptionOption, dueOption, tagsOption
        };
        createTaskCmd.SetHandler(async (string description, string? due, string? tags) =>
        {
            var state = LoadState();
            var task = new TaskRecord
            {
                Description = description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            if (due != null && DateTime.TryParse(due, out var dt)) task.DueDate = dt;
            if (!string.IsNullOrEmpty(tags)) task.Tags = tags.Split(',').Select(t => t.Trim()).Where(t => t.Length>0).ToList();
            state.Tasks.Add(task);
            state.CurrentTaskId = task.Id;
            SaveState(state);
            Console.WriteLine($"Created task {task.Id}");
            await Task.CompletedTask;
        }, descriptionOption, dueOption, tagsOption);

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
                state.CurrentTaskId = null;
                SaveState(state);
                Console.WriteLine("Task completed");
            }
            else
            {
                Console.WriteLine("Task not found");
            }
            await Task.CompletedTask;
        }, completeIdOption, outputTokensOption);

        var failIdOpt = new Option<string>("--id") { IsRequired = true };
        var errorOpt = new Option<string>("--error") { IsRequired = true };
        var failTaskCmd = new Command("fail-task", "Mark a task as failed")
        {
            failIdOpt, errorOpt
        };
        failTaskCmd.SetHandler(async (string id, string error) =>
        {
            var state = LoadState();
            var task = state.Tasks.Find(t => t.Id == id);
            if (task != null)
            {
                task.Status = $"failed:{error}";
                task.UpdatedAt = DateTime.UtcNow;
                state.CurrentTaskId = null;
                SaveState(state);
                Console.WriteLine("Task failed");
            }
            else
            {
                Console.WriteLine("Task not found");
            }
            await Task.CompletedTask;
        }, failIdOpt, errorOpt);

        var currentTaskCmd = new Command("current-task", "Show current task id");
        currentTaskCmd.SetHandler(async () =>
        {
            var state = LoadState();
            Console.WriteLine(state.CurrentTaskId ?? "none");
            await Task.CompletedTask;
        });

        var setCurrentIdOpt = new Option<string>("--id") { IsRequired = true };
        var setCurrentTaskCmd = new Command("set-current-task", "Set current task id")
        {
            setCurrentIdOpt
        };
        setCurrentTaskCmd.SetHandler(async (string id) =>
        {
            var state = LoadState();
            if (state.Tasks.Any(t => t.Id == id))
            {
                state.CurrentTaskId = id;
                SaveState(state);
                Console.WriteLine($"Current task set to {id}");
            }
            else
            {
                Console.WriteLine("Task not found");
            }
            await Task.CompletedTask;
        }, setCurrentIdOpt);

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

        var addOutputTokensCmd = new Command("add-output-tokens", "Increment output tokens")
        {
            addTokensIdOpt, tokensOpt
        };
        addOutputTokensCmd.SetHandler(async (string id, int tokens) =>
        {
            var state = LoadState();
            var task = state.Tasks.Find(t => t.Id == id);
            if (task != null)
            {
                task.OutputTokens += tokens;
                task.UpdatedAt = DateTime.UtcNow;
                SaveState(state);
                Console.WriteLine($"Added {tokens} output tokens");
            }
            else
            {
                Console.WriteLine("Task not found");
            }
            await Task.CompletedTask;
        }, addTokensIdOpt, tokensOpt);

        var taskDurationCmd = new Command("task-duration", "Show task duration") { statsIdOpt };
        taskDurationCmd.SetHandler(async (string id) =>
        {
            var state = LoadState();
            var task = state.Tasks.Find(t => t.Id == id);
            if (task != null)
            {
                var duration = (task.UpdatedAt - task.CreatedAt).TotalSeconds;
                Console.WriteLine(duration);
            }
            else
            {
                Console.WriteLine("Task not found");
            }
            await Task.CompletedTask;
        }, statsIdOpt);

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

        var purgeFailedCmd = new Command("purge-failed-tasks", "Remove failed tasks");
        purgeFailedCmd.SetHandler(async () =>
        {
            var state = LoadState();
            state.Tasks.RemoveAll(t => t.Status == "failed");
            SaveState(state);
            Console.WriteLine("Failed tasks removed");
            await Task.CompletedTask;
        });

        var tasksOverviewCmd = new Command("tasks-overview", "Show count by status");
        tasksOverviewCmd.SetHandler(async () =>
        {
            var state = LoadState();
            var groups = state.Tasks.GroupBy(t => t.Status);
            foreach (var g in groups)
            {
                Console.WriteLine($"{g.Key}: {g.Count()}");
            }
            Console.WriteLine($"Total: {state.Tasks.Count}");
            await Task.CompletedTask;
        });

        var startToolTaskOpt = new Option<string>("--task-id") { IsRequired = true };
        var startToolNameOpt = new Option<string>("--name") { IsRequired = true };
        var startToolCmd = new Command("start-tool", "Begin tool execution")
        {
            startToolTaskOpt, startToolNameOpt
        };
        startToolCmd.SetHandler(async (string taskId, string name) =>
        {
            var state = LoadState();
            var exec = new ToolExecution
            {
                TaskId = taskId,
                Name = name,
                Message = "starting"
            };
            state.ToolExecutions.Add(exec);
            SaveState(state);
            Console.WriteLine(exec.Id);
            await Task.CompletedTask;
        }, startToolTaskOpt, startToolNameOpt);

        var toolIdOpt2 = new Option<string>("--id") { IsRequired = true };
        var msgOpt = new Option<string>("--message") { IsRequired = true };
        var updateToolCmd = new Command("update-tool-progress", "Update tool message")
        {
            toolIdOpt2, msgOpt
        };
        updateToolCmd.SetHandler(async (string id, string message) =>
        {
            var state = LoadState();
            var tool = state.ToolExecutions.Find(t => t.Id == id);
            if (tool != null)
            {
                tool.Message = message;
                SaveState(state);
                Console.WriteLine("updated");
            }
            else
            {
                Console.WriteLine("Tool not found");
            }
            await Task.CompletedTask;
        }, toolIdOpt2, msgOpt);

        var completeToolCmd = new Command("complete-tool", "Finish tool execution")
        {
            toolIdOpt2, msgOpt
        };
        completeToolCmd.SetHandler(async (string id, string message) =>
        {
            var state = LoadState();
            var tool = state.ToolExecutions.Find(t => t.Id == id);
            if (tool != null)
            {
                tool.Message = message;
                tool.Status = "success";
                tool.EndTime = DateTime.UtcNow;
                SaveState(state);
                Console.WriteLine("completed");
            }
            else
            {
                Console.WriteLine("Tool not found");
            }
            await Task.CompletedTask;
        }, toolIdOpt2, msgOpt);

        var failToolCmd = new Command("fail-tool", "Mark tool execution failed")
        {
            toolIdOpt2, msgOpt
        };
        failToolCmd.SetHandler(async (string id, string message) =>
        {
            var state = LoadState();
            var tool = state.ToolExecutions.Find(t => t.Id == id);
            if (tool != null)
            {
                tool.Message = message;
                tool.Status = "error";
                tool.EndTime = DateTime.UtcNow;
                SaveState(state);
                Console.WriteLine("failed");
            }
            else
            {
                Console.WriteLine("Tool not found");
            }
            await Task.CompletedTask;
        }, toolIdOpt2, msgOpt);

        var cleanupToolsCmd = new Command("cleanup-tools", "Remove old tool executions");
        cleanupToolsCmd.SetHandler(async () =>
        {
            var state = LoadState();
            var cutoff = DateTime.UtcNow.AddMinutes(-10);
            state.ToolExecutions.RemoveAll(t => t.EndTime.HasValue && t.EndTime < cutoff);
            SaveState(state);
            Console.WriteLine("cleaned");
            await Task.CompletedTask;
        });

        var listToolsCmd = new Command("list-tools", "List tool executions");
        listToolsCmd.SetHandler(async () =>
        {
            var state = LoadState();
            foreach (var t in state.ToolExecutions)
            {
                Console.WriteLine($"{t.Id}: {t.Name} [{t.Status}] {t.Message}");
            }
            await Task.CompletedTask;
        });

        var toolInfoCmd = new Command("tool-info", "Show tool details") { toolIdOpt2 };
        toolInfoCmd.SetHandler(async (string id) =>
        {
            var state = LoadState();
            var tool = state.ToolExecutions.Find(t => t.Id == id);
            if (tool != null)
            {
                Console.WriteLine(JsonSerializer.Serialize(tool, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.WriteLine("Tool not found");
            }
            await Task.CompletedTask;
        }, toolIdOpt2);

        var toolCountCmd = new Command("tool-count", "Number of tool executions");
        toolCountCmd.SetHandler(async () =>
        {
            var state = LoadState();
            Console.WriteLine(state.ToolExecutions.Count);
            await Task.CompletedTask;
        });

        var runningToolsCmd = new Command("running-tools", "List running tools");
        runningToolsCmd.SetHandler(async () =>
        {
            var state = LoadState();
            foreach (var t in state.ToolExecutions.Where(te => te.Status == "running"))
            {
                Console.WriteLine($"{t.Id}: {t.Name} {t.Message}");
            }
            await Task.CompletedTask;
        });

        var listToolsByTaskCmd = new Command("list-tools-by-task", "List tools for a task") { startToolTaskOpt };
        listToolsByTaskCmd.SetHandler(async (string taskId) =>
        {
            var state = LoadState();
            foreach (var t in state.ToolExecutions.Where(te => te.TaskId == taskId))
            {
                Console.WriteLine($"{t.Id}: {t.Name} [{t.Status}] {t.Message}");
            }
            await Task.CompletedTask;
        }, startToolTaskOpt);

        var deleteToolCmd = new Command("delete-tool", "Remove a tool execution") { toolIdOpt2 };
        deleteToolCmd.SetHandler(async (string id) =>
        {
            var state = LoadState();
            var removed = state.ToolExecutions.RemoveAll(te => te.Id == id);
            SaveState(state);
            Console.WriteLine(removed > 0 ? "deleted" : "not found");
            await Task.CompletedTask;
        }, toolIdOpt2);

        var metaKeyOpt = new Option<string>("--key") { IsRequired = true };
        var metaValOpt = new Option<string>("--value") { IsRequired = true };
        var setToolMetaCmd = new Command("set-tool-metadata", "Set metadata on tool") { toolIdOpt2, metaKeyOpt, metaValOpt };
        setToolMetaCmd.SetHandler(async (string id, string key, string value) =>
        {
            var state = LoadState();
            var tool = state.ToolExecutions.Find(t => t.Id == id);
            if (tool != null)
            {
                tool.Metadata ??= new Dictionary<string, object>();
                tool.Metadata[key] = value;
                SaveState(state);
                Console.WriteLine("metadata set");
            }
            else
            {
                Console.WriteLine("Tool not found");
            }
            await Task.CompletedTask;
        }, toolIdOpt2, metaKeyOpt, metaValOpt);

        var exportToolsPathOpt = new Option<string>("--path") { IsRequired = true };
        var exportToolsCmd = new Command("export-tools", "Save tools to JSON") { exportToolsPathOpt };
        exportToolsCmd.SetHandler(async (string path) =>
        {
            var state = LoadState();
            File.WriteAllText(path, JsonSerializer.Serialize(state.ToolExecutions, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Tools exported to {path}");
            await Task.CompletedTask;
        }, exportToolsPathOpt);

        var importToolsPathOpt = new Option<string>("--path") { IsRequired = true };
        var importToolsCmd = new Command("import-tools", "Load tools from JSON") { importToolsPathOpt };
        importToolsCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return;
            }
            var json = File.ReadAllText(path);
            var tools = JsonSerializer.Deserialize<List<ToolExecution>>(json);
            if (tools != null)
            {
                var state = LoadState();
                state.ToolExecutions = tools;
                SaveState(state);
                Console.WriteLine("Tools imported");
            }
            else
            {
                Console.WriteLine("Invalid tools file");
            }
            await Task.CompletedTask;
        }, importToolsPathOpt);

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

        var subscriptionsCmd = new Command("subscriptions", "List active subscriptions");
        subscriptionsCmd.SetHandler(async () =>
        {
            var state = LoadState();
            foreach (var s in state.Subscriptions) Console.WriteLine(s);
            await Task.CompletedTask;
        });

        var subscriptionCountCmd = new Command("subscription-count", "Show subscription total");
        subscriptionCountCmd.SetHandler(async () =>
        {
            var state = LoadState();
            Console.WriteLine(state.Subscriptions.Count);
            await Task.CompletedTask;
        });

        var rpcStartCmd = new Command("rpc-start", "Start RPC server");
        rpcStartCmd.SetHandler(async () => { RpcServer.Start(); Console.WriteLine("started"); await Task.CompletedTask; });

        var rpcStopCmd = new Command("rpc-stop", "Stop RPC server");
        rpcStopCmd.SetHandler(async () => { RpcServer.Stop(); Console.WriteLine("stopped"); await Task.CompletedTask; });

        var rpcStatusCmd = new Command("rpc-status", "Is RPC server running?");
        rpcStatusCmd.SetHandler(async () => { Console.WriteLine(RpcServer.IsRunning ? "running" : "stopped"); await Task.CompletedTask; });

        var rpcNotifyJsonOpt = new Option<string>("--json") { IsRequired = true };
        var rpcNotifyTypeOpt = new Option<string>("--type", () => "manual");
        var rpcNotifyCmd = new Command("rpc-notify", "Send JSON event") { rpcNotifyJsonOpt, rpcNotifyTypeOpt };
        rpcNotifyCmd.SetHandler(async (string json, string type) =>
        {
            try
            {
                var obj = JsonSerializer.Deserialize<object>(json);
                if (obj != null) RpcServer.Notify(obj, type);
                Console.WriteLine("sent");
            }
            catch { Console.WriteLine("invalid json"); }
            await Task.CompletedTask;
        }, rpcNotifyJsonOpt, rpcNotifyTypeOpt);

        var rpcNotifyFileOpt = new Option<string>("--path") { IsRequired = true };
        var rpcNotifyFileTypeOpt = new Option<string>("--type", () => "manual");
        var rpcNotifyFileCmd = new Command("rpc-notify-file", "Send JSON event from file") { rpcNotifyFileOpt, rpcNotifyFileTypeOpt };
        rpcNotifyFileCmd.SetHandler(async (string path, string type) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("file not found"); return; }
            var json = File.ReadAllText(path);
            try
            {
                var obj = JsonSerializer.Deserialize<object>(json);
                if (obj != null) RpcServer.Notify(obj, type);
                Console.WriteLine("sent");
            }
            catch { Console.WriteLine("invalid json"); }
            await Task.CompletedTask;
        }, rpcNotifyFileOpt, rpcNotifyFileTypeOpt);

        var rpcStreamCmd = new Command("rpc-stream-events", "Stream events from RPC server");
        var rpcTypeOpt2 = new Option<string>("--type", () => string.Empty);
        rpcStreamCmd.AddOption(rpcTypeOpt2);
        rpcStreamCmd.SetHandler(async (string type) =>
        {
            using var client = new HttpClient();
            var url = "http://localhost:5050/stream" + (string.IsNullOrEmpty(type) ? "" : $"?type={type}");
            try
            {
                using var stream = await client.GetStreamAsync(url);
                using var reader = new StreamReader(stream);
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (line != null && line.StartsWith("data:"))
                        Console.WriteLine(line.Substring(5).Trim());
                }
            }
            catch (Exception ex) { Console.WriteLine($"error: {ex.Message}"); }
        }, rpcTypeOpt2);


        var root = new RootCommand("oli .NET CLI")
        {
            runCmd,
            agentCmd,
            agentStatusCmd,
            setModelCmd,
            modelsCmd,
            versionCmd,
            subscribeCmd,
            unsubscribeCmd,
            subscriptionsCmd,
            subscriptionCountCmd,
            rpcStartCmd,
            rpcStopCmd,
            rpcStatusCmd,
            rpcNotifyCmd,
            rpcNotifyFileCmd,
            rpcStreamCmd
        };

        LspCommands.Register(root);
        TaskCommands.Register(root);
        ToolCommands.Register(root);
        BackupCommands.Register(root);
        ConversationCommands.Register(root);
        MemoryCommands.Register(root);
        StateCommands.Register(root);
        AdditionalCommands.Register(root);
        FileCommands.Register(root);
        SummaryCommands.Register(root);
        JsonCommands.Register(root);
        YamlCommands.Register(root);
        ApiKeyCommands.Register(root);
        NetworkCommands.Register(root);
        HistoryCommands.Register(root);
        LogCommands.Register(root);
        PathCommands.Register(root);

        return root.Invoke(args);
    }

    public static string GenerateDiff(string oldContent, string newContent)
    {
        var diff = InlineDiffBuilder.Diff(oldContent, newContent);
        var sb = new System.Text.StringBuilder();
        foreach (var line in diff.Lines)
        {
            if (line.Type == ChangeType.Inserted) sb.AppendLine($"+ {line.Text}");
            else if (line.Type == ChangeType.Deleted) sb.AppendLine($"- {line.Text}");
        }
        return sb.ToString();
    }

    public static async Task AutoCompress(AppState state)
    {
        if (!state.AutoCompress) return;
        var charCount = state.Conversation.Sum(m => m.Length);
        if (state.Conversation.Count >= state.CompressMessageThreshold || charCount >= state.CompressCharThreshold)
        {
            var text = string.Join(" ", state.Conversation);
            string summary;
            try
            {
                summary = await SummarizeAsync(text);
            }
            catch
            {
                summary = GenerateSummary(text);
            }
            state.ConversationSummaries.Add(new ConversationSummary
            {
                Content = summary,
                CreatedAt = DateTime.UtcNow,
                MessagesCount = state.Conversation.Count,
                OriginalChars = text.Length
            });
            state.Conversation.Clear();
        }
    }

    public static int EstimateTokens(string text)
    {
        return text.Length / 4 + 1;
    }

    public static (string? FilePath, int? Lines) ExtractToolMetadata(string message)
    {
        string? filePath = null;
        int? lines = null;
        var pathMatch = new System.Text.RegularExpressions.Regex(@"([\w./\\-]+\.[\w]+)").Match(message);
        if (pathMatch.Success) filePath = pathMatch.Groups[1].Value;
        var lineMatch = new System.Text.RegularExpressions.Regex(@"(\d+)\s*lines?").Match(message);
        if (lineMatch.Success && int.TryParse(lineMatch.Groups[1].Value, out var n)) lines = n;
        return (filePath, lines);
    }

    public static string ToolDescription(string name, string? filePath, int? lines)
    {
        return name switch
        {
            "View" => filePath != null ? (lines.HasValue ? $"Read {lines.Value} lines" : "Reading file contents") : "Reading file",
            "Glob" => "Finding files by pattern",
            "Grep" => "Searching code for pattern",
            "LS" => "Listing directory contents",
            "Edit" => "Modifying file",
            "Replace" => "Replacing file contents",
            "Bash" => "Executing command",
            _ => "Executing tool"
        };
    }

    public static bool ValidateApiKey(string modelName, string apiKey)
    {
        return !(string.IsNullOrEmpty(apiKey) && !modelName.ToLower().Contains("local"));
    }

    public static (string Provider, string AgentModel) DetermineProvider(string modelName, string apiKey, string modelFile)
    {
        var lower = modelName.ToLower();
        string provider = lower.Contains("claude") ? "Anthropic" : lower.Contains("gpt") ? "OpenAI" : lower.Contains("gemini") ? "Gemini" : "Ollama";
        var agentModel = provider switch
        {
            "Anthropic" => "claude-3",
            "OpenAI" => "gpt-4o",
            "Gemini" => "gemini-1.5",
            _ => modelFile
        };
        return (provider, agentModel);
    }

    public static List<string> DisplayToSession(IEnumerable<string> display)
    {
        var result = new List<string>();
        string role = "user";
        foreach (var msg in display)
        {
            if (msg.StartsWith("[user]") || msg.StartsWith("User:")) role = "user";
            else if (msg.StartsWith("[assistant]") || msg.StartsWith("Assistant:")) role = "assistant";
            else if (msg.StartsWith("[system]") || msg.StartsWith("System:")) role = "system";
            else if (msg.StartsWith("[wait]") || msg.StartsWith("[info]") || msg.StartsWith("[success]")) continue;
            var content = msg.Split(']', 2).Last().Trim();
            result.Add($"{role}:{content}");
        }
        return result;
    }

    public static List<string> SessionToDisplay(IEnumerable<string> session)
    {
        return session.Select(s =>
        {
            var parts = s.Split(':', 2);
            var role = parts[0];
            var content = parts.Length > 1 ? parts[1] : string.Empty;
            return role switch
            {
                "user" => $"[user] {content}",
                "assistant" => $"[assistant] {content}",
                "system" => $"[system] {content}",
                _ => content
            };
        }).ToList();
    }

    public static string GenerateSummary(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Take(20));
    }

}
