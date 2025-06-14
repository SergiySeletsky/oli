using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using System.Text.Json.Nodes;
using OLI.NetCli;

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

    static async Task<int> Main(string[] args)
    {
        string serverPath = Environment.GetEnvironmentVariable("BACKEND_BIN_PATH")
            ?? Path.Combine(AppContext.BaseDirectory, "oli-server");

        var promptOption = new Option<string>("--prompt", "Prompt text") { IsRequired = true };
        var modelOption = new Option<int>("--model-index", () => 0, "Index of the model to use");
        var agentOpt = new Option<bool>("--use-agent", () => false, "Enable agent mode for this run");
        var runCmd = new Command("run", "Send a prompt to the assistant")
        {
            promptOption,
            modelOption,
            agentOpt
        };
        runCmd.SetHandler(async (string prompt, int modelIndex, bool useAgent) =>
        {
            using var client = new RpcClient(serverPath);
            var state = LoadState();
            state = state with { SelectedModel = modelIndex, AgentMode = useAgent };
            SaveState(state);

            var result = await client.CallAsync("run", new JsonObject
            {
                ["prompt"] = prompt,
                ["model_index"] = modelIndex,
                ["use_agent"] = useAgent
            });
            Console.WriteLine(result?["response"]?.GetValue<string>() ?? string.Empty);
        }, promptOption, modelOption, agentOpt);

        var enableOption = new Option<bool>("--enable", "Enable or disable agent mode") { IsRequired = true };
        var agentCmd = new Command("agent-mode", "Enable or disable agent mode") { enableOption };
        agentCmd.SetHandler((bool enable) =>
        {
            var state = LoadState();
            state = state with { AgentMode = enable };
            SaveState(state);
            Console.WriteLine($"Agent mode set to {enable}");
        }, enableOption);

        var modelsCmd = new Command("models", "List available models");
        modelsCmd.SetHandler(async () =>
        {
            using var client = new RpcClient(serverPath);
            var result = await client.CallAsync("get_available_models", null);
            var models = result?["models"]?.AsArray();
            if (models is not null)
            {
                int i = 0;
                foreach (var m in models)
                {
                    Console.WriteLine($"[{i++}] {m?["name"]?.GetValue<string>()}");
                }
            }
        });

        var clearCmd = new Command("clear", "Clear conversation history");
        clearCmd.SetHandler(async () =>
        {
            using var client = new RpcClient(serverPath);
            await client.CallAsync("clear_conversation", null);
            Console.WriteLine("Conversation cleared");
        });

        var versionCmd = new Command("version", "Show backend version");
        versionCmd.SetHandler(async () =>
        {
            using var client = new RpcClient(serverPath);
            var result = await client.CallAsync("get_version", null);
            Console.WriteLine(result?["version"]?.GetValue<string>() ?? string.Empty);
        });

        var root = new RootCommand("oli .NET CLI")
        {
            runCmd, agentCmd, modelsCmd, clearCmd, versionCmd
        };

        return await root.InvokeAsync(args);
    }
}
