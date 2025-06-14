using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;

record AppState(bool AgentMode, int SelectedModel);

class Program
{
    static readonly string StatePath = Path.Combine(AppContext.BaseDirectory, "state.json");

    static AppState LoadState()
    {
        if (File.Exists(StatePath))
        {
            var json = File.ReadAllText(StatePath);
            return JsonSerializer.Deserialize<AppState>(json) ?? new AppState(false, 0);
        }
        return new AppState(false, 0);
    }

    static void SaveState(AppState state)
    {
        File.WriteAllText(StatePath, JsonSerializer.Serialize(state));
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
            state = state with { SelectedModel = modelIndex };
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
            state = state with { AgentMode = enable };
            SaveState(state);
            Console.WriteLine($"Agent mode set to {enable}");
        }, enableOption);

        var modelsCmd = new Command("models", "List available models");
        modelsCmd.SetHandler(() =>
        {
            string[] models = ["gpt-4o", "claude-sonnet", "gemini-1.5" ];
            for (int i = 0; i < models.Length; i++)
            {
                Console.WriteLine($"[{i}] {models[i]}");
            }
        });

        var versionCmd = new Command("version", "Display CLI version");
        versionCmd.SetHandler(() =>
        {
            var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";
            Console.WriteLine($"OLI.NetCli version {version}");
        });

        var root = new RootCommand("oli .NET CLI")
        {
            runCmd, agentCmd, modelsCmd, versionCmd
        };

        return root.Invoke(args);
    }
}
