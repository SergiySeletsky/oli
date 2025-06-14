using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public static class LspCommands
{
    public static void Register(RootCommand root)
    {
        var langArg = new Argument<string>("lang");
        var rootArg = new Argument<string>("root");
        var lspStartCmd = new Command("lsp-start", "Start LSP server") { langArg, rootArg };
        lspStartCmd.SetHandler(async (string lang, string root) =>
        {
            var state = Program.LoadState();
            var info = new LspServerInfo { Language = lang, RootPath = Path.GetFullPath(root) };
            state.LspServers.Add(info);
            Program.SaveState(state);
            Console.WriteLine($"Started {lang} server {info.Id} at {info.RootPath}");
            await Task.CompletedTask;
        }, langArg, rootArg);

        var stopIdArg = new Argument<string>("id");
        var lspStopCmd = new Command("lsp-stop", "Stop LSP server") { stopIdArg };
        lspStopCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var removed = state.LspServers.RemoveAll(s => s.Id == id) > 0;
            Program.SaveState(state);
            Console.WriteLine(removed ? $"Stopped {id}" : $"Server {id} not found");
            await Task.CompletedTask;
        }, stopIdArg);

        var restartIdArg = new Argument<string>("id");
        var lspRestartCmd = new Command("lsp-restart", "Restart LSP server") { restartIdArg };
        lspRestartCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var info = state.LspServers.FirstOrDefault(s => s.Id == id);
            if (info == null)
            {
                Console.WriteLine("Server not found");
                return;
            }
            state.LspServers.RemoveAll(s => s.Id == id);
            var newInfo = new LspServerInfo { Language = info.Language, RootPath = info.RootPath };
            state.LspServers.Add(newInfo);
            Program.SaveState(state);
            Console.WriteLine($"Restarted {id} -> {newInfo.Id}");
            await Task.CompletedTask;
        }, restartIdArg);

        var lspListCmd = new Command("lsp-list", "List LSP servers");
        lspListCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            if (state.LspServers.Count == 0) Console.WriteLine("No servers");
            foreach (var s in state.LspServers)
                Console.WriteLine($"{s.Id}: {s.Language} {s.RootPath}");
            await Task.CompletedTask;
        });

        var lspPathCmd = new Command("lsp-path", "Show path to LSP server list");
        lspPathCmd.SetHandler(() => { Console.WriteLine(Program.LspPath); });

        root.Add(lspStartCmd);
        root.Add(lspStopCmd);
        root.Add(lspRestartCmd);
        root.Add(lspListCmd);
        root.Add(lspPathCmd);
    }
}
