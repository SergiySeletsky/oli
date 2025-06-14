using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;

public static class StateCommands
{
    public static void Register(RootCommand root)
    {
        var statePathCmd = new Command("state-path", "Show path of state file");
        statePathCmd.SetHandler(async () => { Console.WriteLine(Program.StatePath); await Task.CompletedTask; });

        var stateInfoCmd = new Command("state-info", "Show summary of saved state");
        stateInfoCmd.SetHandler(async () => {
            var state = Program.LoadState();
            Console.WriteLine($"AgentMode:{state.AgentMode} Model:{state.SelectedModel} Tasks:{state.Tasks.Count} Messages:{state.Conversation.Count}");
            await Task.CompletedTask;
        });

        var stateVersionCmd = new Command("state-version", "Show state file version");
        stateVersionCmd.SetHandler(async () => {
            var state = Program.LoadState();
            Console.WriteLine(state.StateVersion);
            await Task.CompletedTask;
        });

        var stateSummaryCmd = new Command("state-summary", "Show counts of tasks, messages, and summaries");
        stateSummaryCmd.SetHandler(async () => {
            var state = Program.LoadState();
            Console.WriteLine($"tasks:{state.Tasks.Count} messages:{state.Conversation.Count} summaries:{state.ConversationSummaries.Count}");
            await Task.CompletedTask;
        });

        var stateFilesCmd = new Command("state-files", "List all state file paths");
        stateFilesCmd.SetHandler(async () => {
            Console.WriteLine(Program.StatePath);
            Console.WriteLine(Program.TasksPath);
            Console.WriteLine(Program.ConversationPath);
            Console.WriteLine(Program.SummariesPath);
            Console.WriteLine(Program.ToolsPath);
            Console.WriteLine(Program.LspPath);
            await Task.CompletedTask;
        });

        var setWorkingDirOpt = new Option<string>("--path") { IsRequired = true };
        var setWorkingDirCmd = new Command("set-working-dir", "Set working directory") { setWorkingDirOpt };
        setWorkingDirCmd.SetHandler(async (string path) => {
            var state = Program.LoadState();
            state.WorkingDirectory = path;
            Program.SaveState(state);
            Console.WriteLine($"Working directory set to {path}");
            await Task.CompletedTask;
        }, setWorkingDirOpt);

        var currentDirCmd = new Command("current-directory", "Show configured working directory");
        currentDirCmd.SetHandler(async () => {
            var state = Program.LoadState();
            Console.WriteLine(state.WorkingDirectory);
            await Task.CompletedTask;
        });

        root.AddCommand(statePathCmd);
        root.AddCommand(stateInfoCmd);
        root.AddCommand(stateVersionCmd);
        root.AddCommand(stateSummaryCmd);
        root.AddCommand(stateFilesCmd);
        root.AddCommand(setWorkingDirCmd);
        root.AddCommand(currentDirCmd);
    }
}
