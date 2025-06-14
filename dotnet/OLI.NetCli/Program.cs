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

        var versionCmd = new Command("version", "Display CLI version");
        versionCmd.SetHandler(() =>
        {
            var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";
            Console.WriteLine($"OLI.NetCli version {version}");
        });

        var root = new RootCommand("oli .NET CLI")
        {
            runCmd, agentCmd, setModelCmd, modelsCmd, tasksCmd, cancelTaskCmd,
            clearConvCmd, memoryInfoCmd, addMemoryCmd, replaceMemoryCmd, versionCmd
        };

        return root.Invoke(args);
    }
}
