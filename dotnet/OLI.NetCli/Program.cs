using System;
using System.Collections.Generic;
using System.CommandLine;
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
        runCmd.SetHandler((string prompt, int modelIndex) =>
        {
            var state = LoadState();
            state.SelectedModel = modelIndex;
            state.Conversation.Add($"User: {prompt}");
            SaveState(state);
            Console.WriteLine($"[Model {modelIndex}] Prompt: {prompt}");
            // TODO: call model API
        }, promptOption, modelOption);

        var enableOption = new Option<bool>("--enable", "Set to true to enable agent mode") { IsRequired = true };
        var agentCmd = new Command("agent-mode", "Enable or disable agent mode")
        {
            enableOption
        };
        agentCmd.SetHandler((bool enable) =>
        {
            var state = LoadState();
            state.AgentMode = enable;
            SaveState(state);
            Console.WriteLine($"Agent mode set to {enable}");
        }, enableOption);

        var agentStatusCmd = new Command("agent-status", "Show whether agent mode is enabled");
        agentStatusCmd.SetHandler(() =>
        {
            var state = LoadState();
            Console.WriteLine(state.AgentMode ? "Agent mode is ON" : "Agent mode is OFF");
        });

        var setModelCmd = new Command("set-model", "Select the active model")
        {
            modelOption
        };
        setModelCmd.SetHandler((int index) =>
        {
            var state = LoadState();
            state.SelectedModel = index;
            SaveState(state);
            Console.WriteLine($"Selected model {index}");
        }, modelOption);

        var modelsCmd = new Command("models", "List available models");
        modelsCmd.SetHandler(() =>
        {
            string[] models = ["gpt-4o", "claude-sonnet", "gemini-1.5" ];
            for (int i = 0; i < models.Length; i++)
            {
                Console.WriteLine($"[{i}] {models[i]}");
            }
        });

        var tasksCmd = new Command("tasks", "List tasks");
        tasksCmd.SetHandler(() =>
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
        });

        var createTaskCmd = new Command("create-task", "Add a new task")
        {
            new Option<string>("--description") { IsRequired = true }
        };
        createTaskCmd.SetHandler((string description) =>
        {
            var state = LoadState();
            var task = new TaskRecord { Description = description };
            state.Tasks.Add(task);
            SaveState(state);
            Console.WriteLine($"Created task {task.Id}");
        }, createTaskCmd.Options[0]);

        var completeTaskCmd = new Command("complete-task", "Mark a task as completed")
        {
            new Option<string>("--id") { IsRequired = true }
        };
        completeTaskCmd.SetHandler((string id) =>
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
        }, completeTaskCmd.Options[0]);

        var cancelTaskCmd = new Command("cancel-task", "Cancel a task by id")
        {
            new Option<string>("--id", () => string.Empty, "Task id to cancel")
        };
        cancelTaskCmd.SetHandler((string id) =>
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
        }, cancelTaskCmd.Options[0]);

        var clearConvCmd = new Command("clear-conversation", "Clear stored conversation");
        clearConvCmd.SetHandler(() =>
        {
            var state = LoadState();
            state.Conversation.Clear();
            SaveState(state);
            Console.WriteLine("Conversation cleared");
        });

        var conversationCmd = new Command("conversation", "Show conversation history");
        conversationCmd.SetHandler(() =>
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
        });

        var saveConvCmd = new Command("save-conversation", "Save conversation to file")
        {
            new Option<string>("--path") { IsRequired = true }
        };
        saveConvCmd.SetHandler((string path) =>
        {
            var state = LoadState();
            File.WriteAllLines(path, state.Conversation);
            Console.WriteLine($"Conversation saved to {path}");
        }, saveConvCmd.Options[0]);

        var memoryInfoCmd = new Command("memory-info", "Show memory file path and content");
        memoryInfoCmd.SetHandler(() =>
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
        });

        var addMemoryCmd = new Command("add-memory", "Add a memory entry")
        {
            new Option<string>("--section") { IsRequired = true },
            new Option<string>("--memory") { IsRequired = true }
        };
        addMemoryCmd.SetHandler((string section, string memory) =>
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
        }, addMemoryCmd.Options[0], addMemoryCmd.Options[1]);

        var replaceMemoryCmd = new Command("replace-memory-file", "Replace entire memory file")
        {
            new Option<string>("--content") { IsRequired = true }
        };
        replaceMemoryCmd.SetHandler((string content) =>
        {
            File.WriteAllText(MemoryPath, content);
            Console.WriteLine("Memory file replaced");
        }, replaceMemoryCmd.Options[0]);

        var statePathCmd = new Command("state-path", "Show path of state file");
        statePathCmd.SetHandler(() =>
        {
            Console.WriteLine(StatePath);
        });

        var memoryPathCmd = new Command("memory-path", "Show path of memory file");
        memoryPathCmd.SetHandler(() =>
        {
            Console.WriteLine(MemoryPath);
        });

        var createMemoryCmd = new Command("create-memory-file", "Create memory file if missing");
        createMemoryCmd.SetHandler(() =>
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
        });

        var parseMemoryCmd = new Command("parsed-memory", "Show parsed memory sections");
        parseMemoryCmd.SetHandler(() =>
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
        });

        var versionCmd = new Command("version", "Display CLI version");
        versionCmd.SetHandler(() =>
        {
            var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";
            Console.WriteLine($"OLI.NetCli version {version}");
        });

        var subscribeCmd = new Command("subscribe", "Subscribe to an event type")
        {
            new Option<string>("--event") { IsRequired = true }
        };
        subscribeCmd.SetHandler((string ev) =>
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
        }, subscribeCmd.Options[0]);

        var unsubscribeCmd = new Command("unsubscribe", "Unsubscribe from an event type")
        {
            new Option<string>("--event") { IsRequired = true }
        };
        unsubscribeCmd.SetHandler((string ev) =>
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
        }, unsubscribeCmd.Options[0]);

        var root = new RootCommand("oli .NET CLI")
        {
            runCmd, agentCmd, agentStatusCmd, setModelCmd, modelsCmd,
            tasksCmd, createTaskCmd, completeTaskCmd, cancelTaskCmd,
            clearConvCmd, conversationCmd, saveConvCmd,
            memoryInfoCmd, memoryPathCmd, createMemoryCmd,
            addMemoryCmd, replaceMemoryCmd, parseMemoryCmd,
            statePathCmd, versionCmd, subscribeCmd, unsubscribeCmd
        };

        return root.Invoke(args);
    }
}
