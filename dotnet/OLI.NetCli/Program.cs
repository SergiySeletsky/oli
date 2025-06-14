using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;

class Program
{
    static readonly string StatePath = Path.Combine(AppContext.BaseDirectory, "state.json");
    static readonly string TasksPath = Path.Combine(AppContext.BaseDirectory, "tasks.json");
    static readonly string ConversationPath = Path.Combine(AppContext.BaseDirectory, "conversation.json");
    static readonly string SummariesPath = Path.Combine(AppContext.BaseDirectory, "summaries.json");
    static readonly string ToolsPath = Path.Combine(AppContext.BaseDirectory, "tools.json");
    static readonly string MemoryPath = Path.Combine(AppContext.BaseDirectory, "oli.md");
    static readonly string LspPath = Path.Combine(AppContext.BaseDirectory, "lsp.json");

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

    static AppState LoadState()
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

    static void SaveState(AppState state)
    {
        File.WriteAllText(StatePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        SaveTasks(state.Tasks);
        SaveConversation(state.Conversation);
        SaveSummaries(state.ConversationSummaries);
        SaveTools(state.ToolExecutions);
        SaveLspServers(state.LspServers);
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
            AutoCompress(state);
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
            state.CurrentTaskId = task.Id;
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
            AutoCompress(state);
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
            AutoCompress(state);
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

        var convSearchTextOpt = new Option<string>("--text") { IsRequired = true };
        var convSearchCmd = new Command("conversation-search", "Search conversation for text") { convSearchTextOpt };
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
        }, convSearchTextOpt);

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

        var convFirstCmd = new Command("conversation-first", "Show first conversation message");
        convFirstCmd.SetHandler(async () =>
        {
            var state = LoadState();
            if (state.Conversation.Count > 0)
            {
                Console.WriteLine(state.Conversation.First());
            }
            else
            {
                Console.WriteLine("No conversation");
            }
            await Task.CompletedTask;
        });

        var showRangeStartOpt = new Option<int>("--start") { IsRequired = true };
        var showRangeEndOpt = new Option<int>("--end") { IsRequired = true };
        var conversationRangeCmd = new Command("conversation-range", "Show conversation messages in range") { showRangeStartOpt, showRangeEndOpt };
        conversationRangeCmd.SetHandler(async (int start, int end) =>
        {
            var state = LoadState();
            if (start < 0 || end >= state.Conversation.Count || start > end)
            {
                Console.WriteLine("Invalid range");
                return;
            }
            for (int i = start; i <= end; i++)
            {
                Console.WriteLine(state.Conversation[i]);
            }
            await Task.CompletedTask;
        }, showRangeStartOpt, showRangeEndOpt);

        var conversationInfoCmd = new Command("conversation-info", "Show conversation statistics and ends");
        conversationInfoCmd.SetHandler(async () =>
        {
            var state = LoadState();
            var count = state.Conversation.Count;
            var chars = state.Conversation.Sum(m => m.Length);
            var first = count > 0 ? state.Conversation.First() : string.Empty;
            var last = count > 0 ? state.Conversation.Last() : string.Empty;
            Console.WriteLine($"count:{count} chars:{chars}\nfirst:{first}\nlast:{last}");
            await Task.CompletedTask;
        });

        var listConvCmd = new Command("list-conversation", "List conversation messages");
        listConvCmd.SetHandler(async () =>
        {
            var state = LoadState();
            for (int i = 0; i < state.Conversation.Count; i++)
            {
                Console.WriteLine($"[{i}] {state.Conversation[i]}");
            }
            await Task.CompletedTask;
        });

        var atIndexOpt = new Option<int>("--index") { IsRequired = true };
        var conversationAtCmd = new Command("conversation-at", "Show message at index") { atIndexOpt };
        conversationAtCmd.SetHandler(async (int index) =>
        {
            var state = LoadState();
            if (index >= 0 && index < state.Conversation.Count)
                Console.WriteLine(state.Conversation[index]);
            else
                Console.WriteLine("Invalid index");
            await Task.CompletedTask;
        }, atIndexOpt);

        var delBeforeOpt = new Option<int>("--index") { IsRequired = true };
        var deleteBeforeCmd = new Command("delete-conversation-before", "Delete messages before index") { delBeforeOpt };
        deleteBeforeCmd.SetHandler(async (int index) =>
        {
            var state = LoadState();
            if (index >= 0 && index < state.Conversation.Count)
            {
                state.Conversation.RemoveRange(0, index);
                SaveState(state);
                Console.WriteLine("Messages removed");
            }
            else
            {
                Console.WriteLine("Invalid index");
            }
            await Task.CompletedTask;
        }, delBeforeOpt);

        var delAfterOpt = new Option<int>("--index") { IsRequired = true };
        var deleteAfterCmd = new Command("delete-conversation-after", "Delete messages after index") { delAfterOpt };
        deleteAfterCmd.SetHandler(async (int index) =>
        {
            var state = LoadState();
            if (index >= 0 && index < state.Conversation.Count)
            {
                state.Conversation.RemoveRange(index + 1, state.Conversation.Count - index - 1);
                SaveState(state);
                Console.WriteLine("Messages removed");
            }
            else
            {
                Console.WriteLine("Invalid index");
            }
            await Task.CompletedTask;
        }, delAfterOpt);

        var delContainsOpt = new Option<string>("--text") { IsRequired = true };
        var deleteContainsCmd = new Command("delete-conversation-contains", "Delete messages containing text") { delContainsOpt };
        deleteContainsCmd.SetHandler(async (string text) =>
        {
            var state = LoadState();
            int before = state.Conversation.Count;
            state.Conversation = state.Conversation.Where(m => !m.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
            SaveState(state);
            Console.WriteLine(before - state.Conversation.Count);
            await Task.CompletedTask;
        }, delContainsOpt);

        var reverseConvCmd = new Command("reverse-conversation", "Reverse conversation order");
        reverseConvCmd.SetHandler(async () =>
        {
            var state = LoadState();
            state.Conversation.Reverse();
            SaveState(state);
            Console.WriteLine("Conversation reversed");
            await Task.CompletedTask;
        });
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

        var mergeMemoryPathOpt = new Option<string>("--path") { IsRequired = true };
        var mergeMemoryCmd = new Command("merge-memory-file", "Merge another memory file")
        {
            mergeMemoryPathOpt
        };
        mergeMemoryCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return;
            }
            var existing = File.Exists(MemoryPath) ? File.ReadAllText(MemoryPath) : string.Empty;
            var extra = File.ReadAllText(path);
            File.WriteAllText(MemoryPath, existing + extra);
            Console.WriteLine("Memory files merged");
            await Task.CompletedTask;
        }, mergeMemoryPathOpt);

        var resetMemoryCmd = new Command("reset-memory-file", "Delete and recreate memory file");
        resetMemoryCmd.SetHandler(async () =>
        {
            var template = "# oli.md\n\n## Project Structure\n";
            File.WriteAllText(MemoryPath, template);
            Console.WriteLine("Memory file reset");
            await Task.CompletedTask;
        });

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

        var setWorkingDirOpt = new Option<string>("--path") { IsRequired = true };
        var setWorkingDirCmd = new Command("set-working-dir", "Set working directory") { setWorkingDirOpt };
        setWorkingDirCmd.SetHandler(async (string path) =>
        {
            var state = LoadState();
            state.WorkingDirectory = path;
            SaveState(state);
            Console.WriteLine($"Working directory set to {path}");
            await Task.CompletedTask;
        }, setWorkingDirOpt);

        var currentDirCmd = new Command("current-directory", "Show configured working directory");
        currentDirCmd.SetHandler(async () =>
        {
            var state = LoadState();
            Console.WriteLine(state.WorkingDirectory);
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

        var copySectionOpt = new Option<string>("--section") { IsRequired = true };
        var copyDestOpt = new Option<string>("--dest") { IsRequired = true };
        var copySectionCmd = new Command("copy-memory-section", "Copy memory section to file") { copySectionOpt, copyDestOpt };
        copySectionCmd.SetHandler(async (string section, string dest) =>
        {
            if (!File.Exists(MemoryPath)) { Console.WriteLine("No memory file"); return; }
            var lines = File.ReadAllLines(MemoryPath);
            var collecting = false;
            var selected = new List<string>();
            foreach (var line in lines)
            {
                if (line.StartsWith("## "))
                {
                    collecting = line[3..].Trim() == section;
                    continue;
                }
                if (collecting && line.StartsWith("- "))
                {
                    selected.Add(line);
                }
            }
            File.WriteAllLines(dest, selected);
            Console.WriteLine($"Section copied to {dest}");
            await Task.CompletedTask;
        }, copySectionOpt, copyDestOpt);

        var swapAOpt = new Option<string>("--first") { IsRequired = true };
        var swapBOpt = new Option<string>("--second") { IsRequired = true };
        var swapSectionCmd = new Command("swap-memory-sections", "Swap two memory sections") { swapAOpt, swapBOpt };
        swapSectionCmd.SetHandler(async (string first, string second) =>
        {
            if (!File.Exists(MemoryPath)) { Console.WriteLine("No memory file"); return; }
            var lines = File.ReadAllLines(MemoryPath).ToList();
            int idxA = lines.FindIndex(l => l.StartsWith("## "+first));
            int idxB = lines.FindIndex(l => l.StartsWith("## "+second));
            if (idxA == -1 || idxB == -1) { Console.WriteLine("Section not found"); return; }
            if (idxA > idxB) { var t = idxA; idxA = idxB; idxB = t; var name = first; first = second; second = name; }
            int endA = lines.Skip(idxA+1).TakeWhile(l => !l.StartsWith("## ")).Count();
            int endB = lines.Skip(idxB+1).TakeWhile(l => !l.StartsWith("## ")).Count();
            var secA = lines.GetRange(idxA, endA+1);
            var secB = lines.GetRange(idxB, endB+1);
            lines.RemoveRange(idxB, endB+1);
            lines.InsertRange(idxB, secA);
            lines.RemoveRange(idxA, endA+1);
            lines.InsertRange(idxA, secB);
            File.WriteAllLines(MemoryPath, lines);
            Console.WriteLine("Sections swapped");
            await Task.CompletedTask;
        }, swapAOpt, swapBOpt);

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

        var memorySizeCmd = new Command("memory-size", "Show memory file size");
        memorySizeCmd.SetHandler(async () =>
        {
            var size = File.Exists(MemoryPath) ? new FileInfo(MemoryPath).Length : 0;
            Console.WriteLine(size);
            await Task.CompletedTask;
        });

        var memSearchTextOpt = new Option<string>("--text") { IsRequired = true };
        var searchMemoryCmd = new Command("search-memory", "Search memory for text") { memSearchTextOpt };
        searchMemoryCmd.SetHandler(async (string text) =>
        {
            if (!File.Exists(MemoryPath))
            {
                Console.WriteLine("No memory file found");
                return;
            }
            foreach (var line in File.ReadLines(MemoryPath))
            {
                if (line.Contains(text, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(line);
                }
            }
            await Task.CompletedTask;
        }, memSearchTextOpt);

        var deletePatternOpt = new Option<string>("--pattern") { IsRequired = true };
        var deleteMemoryLineCmd = new Command("delete-memory-lines", "Remove memory lines matching pattern") { deletePatternOpt };
        deleteMemoryLineCmd.SetHandler(async (string pattern) =>
        {
            if (!File.Exists(MemoryPath))
            {
                Console.WriteLine("No memory file found");
                return;
            }
            var lines = File.ReadAllLines(MemoryPath).ToList();
            var remaining = lines.Where(l => !l.Contains(pattern, StringComparison.OrdinalIgnoreCase)).ToList();
            File.WriteAllLines(MemoryPath, remaining);
            Console.WriteLine(lines.Count - remaining.Count);
            await Task.CompletedTask;
        }, deletePatternOpt);
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

        var setAutoCompressOption = new Option<bool>("--enable") { IsRequired = true };
        var setAutoCompressCmd = new Command("set-auto-compress", "Enable or disable automatic compression") { setAutoCompressOption };
        setAutoCompressCmd.SetHandler(async (bool enable) =>
        {
            var state = LoadState();
            state.AutoCompress = enable;
            SaveState(state);
            Console.WriteLine($"Auto compress set to {enable}");
            await Task.CompletedTask;
        }, setAutoCompressOption);

        var threshCharOpt = new Option<int>("--char", () => 4000);
        var threshMsgOpt = new Option<int>("--messages", () => 50);
        var setThresholdsCmd = new Command("set-compress-thresholds", "Configure compression thresholds") { threshCharOpt, threshMsgOpt };
        setThresholdsCmd.SetHandler(async (int ch, int msg) =>
        {
            var state = LoadState();
            state.CompressCharThreshold = ch;
            state.CompressMessageThreshold = msg;
            SaveState(state);
            Console.WriteLine("Thresholds updated");
            await Task.CompletedTask;
        }, threshCharOpt, threshMsgOpt);

        var showConfigCmd = new Command("show-config", "Display configuration settings");
        showConfigCmd.SetHandler(async () =>
        {
            var state = LoadState();
            Console.WriteLine($"Model:{state.SelectedModel} AgentMode:{state.AgentMode} AutoCompress:{state.AutoCompress} CharThresh:{state.CompressCharThreshold} MsgThresh:{state.CompressMessageThreshold} WorkingDir:{state.WorkingDirectory}");
            await Task.CompletedTask;
        });

        var textOpt = new Option<string>("--text") { IsRequired = true };
        var estimateTokensCmd = new Command("estimate-tokens", "Rough token estimate") { textOpt };
        estimateTokensCmd.SetHandler(async (string text) =>
        {
            Console.WriteLine(EstimateTokens(text));
            await Task.CompletedTask;
        }, textOpt);

        var metaMsgOpt = new Option<string>("--message") { IsRequired = true };
        var extractMetaCmd = new Command("extract-metadata", "Parse file path and line count") { metaMsgOpt };
        extractMetaCmd.SetHandler(async (string message) =>
        {
            var (file, lines) = ExtractToolMetadata(message);
            Console.WriteLine(JsonSerializer.Serialize(new { file, lines }));
            await Task.CompletedTask;
        }, metaMsgOpt);

        var toolNameOpt = new Option<string>("--name") { IsRequired = true };
        var fileOpt = new Option<string?>("--file");
        var linesOpt = new Option<int?>("--lines");
        var toolDescCmd = new Command("tool-description", "Get description for a tool") { toolNameOpt, fileOpt, linesOpt };
        toolDescCmd.SetHandler(async (string name, string? file, int? lines) =>
        {
            Console.WriteLine(ToolDescription(name, file, lines));
            await Task.CompletedTask;
        }, toolNameOpt, fileOpt, linesOpt);

        var hasActiveCmd = new Command("has-active-tasks", "Any tasks in progress?");
        hasActiveCmd.SetHandler(async () =>
        {
            var state = LoadState();
            Console.WriteLine(state.Tasks.Any(t => t.Status == "in-progress") ? "true" : "false");
            await Task.CompletedTask;
        });

        var taskStatusesCmd = new Command("task-statuses", "JSON of task statuses");
        taskStatusesCmd.SetHandler(async () =>
        {
            var state = LoadState();
            var statuses = state.Tasks.Select(t => new { t.Id, t.Description, t.Status, t.ToolCount, t.InputTokens, t.OutputTokens, CreatedAt = t.CreatedAt, UpdatedAt = t.UpdatedAt });
            Console.WriteLine(JsonSerializer.Serialize(statuses, new JsonSerializerOptions { WriteIndented = true }));
            await Task.CompletedTask;
        });

        var keyModelOpt = new Option<string>("--model") { IsRequired = true };
        var apiKeyOpt = new Option<string>("--key") { IsRequired = true };
        var validateKeyCmd = new Command("validate-api-key", "Check API key for model") { keyModelOpt, apiKeyOpt };
        validateKeyCmd.SetHandler(async (string model, string key) =>
        {
            Console.WriteLine(ValidateApiKey(model, key) ? "valid" : "invalid");
            await Task.CompletedTask;
        }, keyModelOpt, apiKeyOpt);

        var fileNameOpt = new Option<string>("--file") { IsRequired = true };
        var determineProviderCmd = new Command("determine-provider", "Show provider and agent model") { keyModelOpt, apiKeyOpt, fileNameOpt };
        determineProviderCmd.SetHandler(async (string model, string key, string file) =>
        {
            var (prov, agent) = DetermineProvider(model, key, file);
            Console.WriteLine($"{prov}:{agent}");
            await Task.CompletedTask;
        }, keyModelOpt, apiKeyOpt, fileNameOpt);

        var displayPathOpt = new Option<string>("--path") { IsRequired = true };
        var displayToSessionCmd = new Command("display-to-session", "Convert display messages to session format") { displayPathOpt };
        displayToSessionCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }
            var lines = File.ReadAllLines(path);
            var session = DisplayToSession(lines);
            Console.WriteLine(JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true }));
            await Task.CompletedTask;
        }, displayPathOpt);

        var sessionPathOpt = new Option<string>("--path") { IsRequired = true };
        var sessionToDisplayCmd = new Command("session-to-display", "Convert session messages to display format") { sessionPathOpt };
        sessionToDisplayCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }
            var lines = File.ReadAllLines(path);
            var display = SessionToDisplay(lines);
            Console.WriteLine(string.Join("\n", display));
            await Task.CompletedTask;
        }, sessionPathOpt);

        var summarizeTextOpt = new Option<string>("--text") { IsRequired = true };
        var summarizeTextCmd = new Command("summarize-text", "Summarize provided text") { summarizeTextOpt };
        summarizeTextCmd.SetHandler(async (string text) =>
        {
            Console.WriteLine(GenerateSummary(text));
            await Task.CompletedTask;
        }, summarizeTextOpt);

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

        var latestSummaryCmd = new Command("latest-summary", "Show the latest summary");
        latestSummaryCmd.SetHandler(async () =>
        {
            var state = LoadState();
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
            var state = LoadState();
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

        var startRangeOpt = new Option<int>("--start") { IsRequired = true };
        var endRangeOpt = new Option<int>("--end") { IsRequired = true };
        var deleteSummaryRangeCmd = new Command("delete-summary-range", "Delete summaries in range") { startRangeOpt, endRangeOpt };
        deleteSummaryRangeCmd.SetHandler(async (int start, int end) =>
        {
            var state = LoadState();
            if (start >= 0 && end >= start && end < state.ConversationSummaries.Count)
            {
                state.ConversationSummaries.RemoveRange(start, end - start + 1);
                SaveState(state);
                Console.WriteLine("Summaries removed");
            }
            else
            {
                Console.WriteLine("Invalid range");
            }
            await Task.CompletedTask;
        }, startRangeOpt, endRangeOpt);

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

        var listDirRecursiveCmd = new Command("list-directory-recursive", "List directory recursively") { dirPathOption };
        listDirRecursiveCmd.SetHandler(async (string path) =>
        {
            if (!Directory.Exists(path))
            {
                Console.WriteLine("Directory not found");
                return;
            }
            foreach (var entry in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories))
            {
                Console.WriteLine(entry);
            }
            await Task.CompletedTask;
        }, dirPathOption);

        var headLinesOpt = new Option<int>("--lines", () => 10);
        var headFileCmd = new Command("head-file", "Read first lines of file") { readPathOption, headLinesOpt };
        headFileCmd.SetHandler(async (string path, int lines) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return;
            }
            foreach (var line in File.ReadLines(path).Take(lines))
            {
                Console.WriteLine(line);
            }
            await Task.CompletedTask;
        }, readPathOption, headLinesOpt);

        var tailLinesOpt = new Option<int>("--lines", () => 10);
        var tailFileCmd = new Command("tail-file", "Read last lines of file") { readPathOption, tailLinesOpt };
        tailFileCmd.SetHandler(async (string path, int lines) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return;
            }
            var allLines = File.ReadAllLines(path);
            foreach (var line in allLines.TakeLast(lines))
            {
                Console.WriteLine(line);
            }
            await Task.CompletedTask;
        }, readPathOption, tailLinesOpt);

        var fileSizeCmd = new Command("file-size", "Show size of a file") { readPathOption };
        fileSizeCmd.SetHandler(async (string path) =>
        {
            var size = File.Exists(path) ? new FileInfo(path).Length : 0;
            Console.WriteLine(size);
            await Task.CompletedTask;
        }, readPathOption);
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

        var subscriptionCountCmd = new Command("subscription-count", "Number of active subscriptions");
        subscriptionCountCmd.SetHandler(() =>
        {
            var state = LoadState();
            Console.WriteLine(state.Subscriptions.Count);
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

        var tasksPathCmd = new Command("tasks-path", "Show tasks file path");
        tasksPathCmd.SetHandler(async () =>
        {
            Console.WriteLine(TasksPath);
            await Task.CompletedTask;
        });

        var conversationPathCmd = new Command("conversation-path", "Show conversation file path");
        conversationPathCmd.SetHandler(async () =>
        {
            Console.WriteLine(ConversationPath);
            await Task.CompletedTask;
        });

        var summariesPathCmd = new Command("summaries-path", "Show summaries file path");
        summariesPathCmd.SetHandler(async () =>
        {
            Console.WriteLine(SummariesPath);
            await Task.CompletedTask;
        });

        var toolsPathCmd = new Command("tools-path", "Show tools file path");
        toolsPathCmd.SetHandler(async () =>
        {
            Console.WriteLine(ToolsPath);
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

        var lspLangOpt = new Option<string>("--lang", "Language (python/rust)") { IsRequired = true };
        var lspRootOpt = new Option<string>("--root", () => Directory.GetCurrentDirectory(), "Workspace root");
        var lspIdOpt = new Option<string>("--id", "LSP server id") { IsRequired = true };
        var lspFileOpt = new Option<string>("--file", "File path") { IsRequired = true };
        var lspNameOpt = new Option<string>("--name", "Symbol name") { IsRequired = true };

        var lspStartCmd = new Command("lsp-start", "Start LSP server") { lspLangOpt, lspRootOpt };
        lspStartCmd.SetHandler(async (string lang, string root) =>
        {
            var state = LoadState();
            var info = new LspServerInfo { Language = lang, RootPath = Path.GetFullPath(root) };
            state.LspServers.Add(info);
            SaveState(state);
            Console.WriteLine($"Started {lang} server {info.Id} at {info.RootPath}");
            await Task.CompletedTask;
        }, lspLangOpt, lspRootOpt);

        var lspStopCmd = new Command("lsp-stop", "Stop LSP server") { lspIdOpt };
        lspStopCmd.SetHandler(async (string id) =>
        {
            var state = LoadState();
            var removed = state.LspServers.RemoveAll(s => s.Id == id) > 0;
            SaveState(state);
            Console.WriteLine(removed ? $"Stopped {id}" : $"Server {id} not found");
            await Task.CompletedTask;
        }, lspIdOpt);

        var lspStopAllCmd = new Command("lsp-stop-all", "Stop all LSP servers");
        lspStopAllCmd.SetHandler(async () =>
        {
            var state = LoadState();
            state.LspServers.Clear();
            SaveState(state);
            Console.WriteLine("Stopped all servers");
            await Task.CompletedTask;
        });

        var lspListCmd = new Command("lsp-list", "List LSP servers");
        lspListCmd.SetHandler(async () =>
        {
            var state = LoadState();
            if (state.LspServers.Count == 0) Console.WriteLine("No servers");
            foreach (var s in state.LspServers)
                Console.WriteLine($"{s.Id}: {s.Language} {s.RootPath}");
            await Task.CompletedTask;
        });

        var lspInfoCmd = new Command("lsp-info", "Show server info") { lspIdOpt };
        lspInfoCmd.SetHandler(async (string id) =>
        {
            var state = LoadState();
            var s = state.LspServers.FirstOrDefault(x => x.Id == id);
            if (s == null) Console.WriteLine("Not found");
            else Console.WriteLine($"{s.Id}: {s.Language} started {s.StartedAt:u} root {s.RootPath}");
            await Task.CompletedTask;
        }, lspIdOpt);

        var lspSymbolsCmd = new Command("lsp-symbols", "List symbols") { lspFileOpt };
        lspSymbolsCmd.SetHandler(async (string file) =>
        {
            foreach (var sym in ExtractDocumentSymbols(file))
                Console.WriteLine($"{sym.Line}:{sym.Name}");
            await Task.CompletedTask;
        }, lspFileOpt);

        var lspCodeLensCmd = new Command("lsp-codelens", "Show codelens") { lspFileOpt };
        lspCodeLensCmd.SetHandler(async (string file) =>
        {
            foreach (var sym in ExtractCodeLens(file))
                Console.WriteLine($"{sym.Line}:{sym.Name}");
            await Task.CompletedTask;
        }, lspFileOpt);

        var lspTokensCmd = new Command("lsp-semantic-tokens", "Count tokens") { lspFileOpt };
        lspTokensCmd.SetHandler(async (string file) =>
        {
            Console.WriteLine(CountSemanticTokens(file));
            await Task.CompletedTask;
        }, lspFileOpt);

        var lspDefCmd = new Command("lsp-definition", "Find definition") { lspFileOpt, lspNameOpt };
        lspDefCmd.SetHandler(async (string file, string name) =>
        {
            var line = FindDefinitionLine(file, name);
            Console.WriteLine(line >= 0 ? $"{file}:{line}" : "not found");
            await Task.CompletedTask;
        }, lspFileOpt, lspNameOpt);

        var lspRootCmd = new Command("lsp-workspace-root", "Find workspace root") { lspFileOpt };
        lspRootCmd.SetHandler(async (string file) =>
        {
            Console.WriteLine(FindWorkspaceRoot(file));
            await Task.CompletedTask;
        }, lspFileOpt);

        var root = new RootCommand("oli .NET CLI")
        {
            runCmd, agentCmd, agentStatusCmd, setModelCmd, modelsCmd,
            tasksCmd, createTaskCmd, completeTaskCmd, failTaskCmd, currentTaskCmd, setCurrentTaskCmd, cancelTaskCmd,
            clearConvCmd, conversationCmd, saveConvCmd,
            memoryInfoCmd, memoryPathCmd, createMemoryCmd,
            addMemoryCmd, replaceMemoryCmd, parseMemoryCmd,
            sectionCountCmd, entryCountCmd, memoryTemplateCmd, memorySizeCmd, searchMemoryCmd, deleteMemoryLineCmd, mergeMemoryCmd, resetMemoryCmd, copySectionCmd, swapSectionCmd,
            summarizeCmd, convStatsCmd,
            convCharCountCmd, summaryCountCmd, compressConvCmd,
            clearHistoryCmd, showSummariesCmd, exportSummariesCmd,
            importSummariesCmd, deleteSummaryCmd,
            setAutoCompressCmd, setThresholdsCmd, showConfigCmd,
            estimateTokensCmd, extractMetaCmd, toolDescCmd, hasActiveCmd,
            taskStatusesCmd, validateKeyCmd, determineProviderCmd, displayToSessionCmd,
            sessionToDisplayCmd, summarizeTextCmd,
            readFileCmd, readNumberedCmd, readLinesCmd,
            writeFileCmd, writeDiffCmd, editFileCmd, appendFileCmd,
            genWriteDiffCmd, genEditDiffCmd, copyFileCmd, moveFileCmd, renameFileCmd,
            deleteFileCmd, fileExistsCmd, listDirCmd, listDirRecursiveCmd, headFileCmd, tailFileCmd, fileSizeCmd, createDirCmd, deleteDirCmd, dirExistsCmd, fileInfoCmd, countLinesCmd,
            globSearchCmd, globSearchInDirCmd, grepSearchCmd,
            currentModelCmd, listSubsCmd, deleteMemorySectionCmd,
            deleteTaskCmd, taskInfoCmd, taskStatsCmd,
            addInputTokensCmd, addToolUseCmd, startToolCmd, updateToolCmd, completeToolCmd, failToolCmd, cleanupToolsCmd, listToolsCmd, toolInfoCmd, toolCountCmd, runningToolsCmd, listToolsByTaskCmd, deleteToolCmd, setToolMetaCmd, exportToolsCmd, importToolsCmd, resetStateCmd,
            importStateCmd, exportStateCmd, deleteMemoryFileCmd,
            listMemorySectionsCmd, tasksPathCmd, conversationPathCmd, summariesPathCmd, toolsPathCmd,
            appendMemoryCmd, importMemoryCmd, exportMemoryCmd, statePathCmd, stateInfoCmd, versionCmd,
            memoryExistsCmd, subscribeCmd, unsubscribeCmd, subscriptionCountCmd,
            taskCountCmd, clearTasksCmd, clearCompletedCmd, tasksByStatusCmd, updateTaskDescCmd, exportTasksCmd,
            importTasksCmd, importConvCmd, appendConvCmd, convLenCmd, lastConvCmd, convSearchCmd, deleteRangeCmd, convFirstCmd, conversationRangeCmd, conversationInfoCmd, listConvCmd, conversationAtCmd, deleteBeforeCmd, deleteAfterCmd, deleteContainsCmd, reverseConvCmd, exportConvCmd, deleteConvMsgCmd,
            latestSummaryCmd, summaryInfoCmd, deleteSummaryRangeCmd,
            addOutputTokensCmd, taskDurationCmd,
            setWorkingDirCmd, currentDirCmd,
            lspStartCmd, lspStopCmd, lspStopAllCmd, lspListCmd, lspInfoCmd,
            lspSymbolsCmd, lspCodeLensCmd, lspTokensCmd, lspDefCmd, lspRootCmd
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

    static void AutoCompress(AppState state)
    {
        if (!state.AutoCompress) return;
        var charCount = state.Conversation.Sum(m => m.Length);
        if (state.Conversation.Count >= state.CompressMessageThreshold || charCount >= state.CompressCharThreshold)
        {
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
        }
    }

    static int EstimateTokens(string text)
    {
        return text.Length / 4 + 1;
    }

    static (string? FilePath, int? Lines) ExtractToolMetadata(string message)
    {
        string? filePath = null;
        int? lines = null;
        var pathMatch = new System.Text.RegularExpressions.Regex(@"([\w./\\-]+\.[\w]+)").Match(message);
        if (pathMatch.Success) filePath = pathMatch.Groups[1].Value;
        var lineMatch = new System.Text.RegularExpressions.Regex(@"(\d+)\s*lines?").Match(message);
        if (lineMatch.Success && int.TryParse(lineMatch.Groups[1].Value, out var n)) lines = n;
        return (filePath, lines);
    }

    static string ToolDescription(string name, string? filePath, int? lines)
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

    static bool ValidateApiKey(string modelName, string apiKey)
    {
        return !(string.IsNullOrEmpty(apiKey) && !modelName.ToLower().Contains("local"));
    }

    static (string Provider, string AgentModel) DetermineProvider(string modelName, string apiKey, string modelFile)
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

    static List<string> DisplayToSession(IEnumerable<string> display)
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

    static List<string> SessionToDisplay(IEnumerable<string> session)
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

    static string GenerateSummary(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Take(20));
    }

    static string FindWorkspaceRoot(string filePath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? ".";
        var current = new DirectoryInfo(dir);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Cargo.toml")) ||
                File.Exists(Path.Combine(current.FullName, "pyproject.toml")) ||
                Directory.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        return dir;
    }

    static List<(string Name, int Line)> ExtractDocumentSymbols(string path)
    {
        var result = new List<(string, int)>();
        if (!File.Exists(path)) return result;
        var lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
        {
            var l = lines[i].Trim();
            if (l.StartsWith("class ") || l.StartsWith("struct ") || l.StartsWith("enum "))
                result.Add((l.Split(' ')[1].Split('{', ':')[0], i + 1));
            if (l.StartsWith("def ") || l.StartsWith("fn "))
                result.Add((l.Split(' ')[1].Split('(')[0], i + 1));
        }
        return result;
    }

    static List<(string Name, int Line)> ExtractCodeLens(string path) => ExtractDocumentSymbols(path);

    static int CountSemanticTokens(string path)
    {
        if (!File.Exists(path)) return 0;
        var text = File.ReadAllText(path);
        return text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    static int FindDefinitionLine(string path, string name)
    {
        var lines = File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>();
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(name)) return i + 1;
        }
        return -1;
    }
}
