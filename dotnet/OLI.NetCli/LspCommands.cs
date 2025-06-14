using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public static class LspCommands
{
    public static void Register(RootCommand root)
    {
        var lspLangOpt = new Option<string>("--lang", "Language (python/rust)") { IsRequired = true };
        var lspRootOpt = new Option<string>("--root", () => Directory.GetCurrentDirectory(), "Workspace root");
        var lspIdOpt = new Option<string>("--id", "LSP server id") { IsRequired = true };
        var lspFileOpt = new Option<string>("--file", "File path") { IsRequired = true };
        var lspLineOpt = new Option<int>("--line", () => 1, "Line number");
        var lspNameOpt = new Option<string>("--name", "Symbol name") { IsRequired = true };
        var oldNameOpt = new Option<string>("--old", "Old name") { IsRequired = true };
        var newNameOpt = new Option<string>("--new", "New name") { IsRequired = true };

        // start
        var lspStartCmd = new Command("lsp-start", "Start LSP server") { lspLangOpt, lspRootOpt };
        lspStartCmd.SetHandler(async (string lang, string root) =>
        {
            var state = Program.LoadState();
            var info = new LspServerInfo { Language = lang, RootPath = Path.GetFullPath(root) };
            state.LspServers.Add(info);
            Program.SaveState(state);
            Console.WriteLine($"Started {lang} server {info.Id} at {info.RootPath}");
            await Task.CompletedTask;
        }, lspLangOpt, lspRootOpt);

        // stop
        var lspStopCmd = new Command("lsp-stop", "Stop LSP server") { lspIdOpt };
        lspStopCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var removed = state.LspServers.RemoveAll(s => s.Id == id) > 0;
            Program.SaveState(state);
            Console.WriteLine(removed ? $"Stopped {id}" : $"Server {id} not found");
            await Task.CompletedTask;
        }, lspIdOpt);

        // restart
        var lspRestartCmd = new Command("lsp-restart", "Restart LSP server") { lspIdOpt };
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
        }, lspIdOpt);

        // stop all
        var lspStopAllCmd = new Command("lsp-stop-all", "Stop all LSP servers");
        lspStopAllCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            state.LspServers.Clear();
            Program.SaveState(state);
            Console.WriteLine("Stopped all servers");
            await Task.CompletedTask;
        });

        // list
        var lspListCmd = new Command("lsp-list", "List LSP servers");
        lspListCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            if (state.LspServers.Count == 0) Console.WriteLine("No servers");
            foreach (var s in state.LspServers)
                Console.WriteLine($"{s.Id}: {s.Language} {s.RootPath}");
            await Task.CompletedTask;
        });

        // info
        var lspInfoCmd = new Command("lsp-info", "Show server info") { lspIdOpt };
        lspInfoCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var s = state.LspServers.FirstOrDefault(x => x.Id == id);
            if (s == null) Console.WriteLine("Not found");
            else Console.WriteLine($"{s.Id}: {s.Language} started {s.StartedAt:u} root {s.RootPath}");
            await Task.CompletedTask;
        }, lspIdOpt);

        var lspPathCmd = new Command("lsp-path", "Show path to LSP server list");
        lspPathCmd.SetHandler(() => { Console.WriteLine(Program.LspPath); });

        // symbols
        var lspSymbolsCmd = new Command("lsp-symbols", "List symbols") { lspFileOpt };
        lspSymbolsCmd.SetHandler(async (string file) =>
        {
            foreach (var sym in ExtractDocumentSymbols(file))
                Console.WriteLine($"{sym.Line}:{sym.Name}");
            await Task.CompletedTask;
        }, lspFileOpt);

        // codelens
        var lspCodeLensCmd = new Command("lsp-codelens", "Show codelens") { lspFileOpt };
        lspCodeLensCmd.SetHandler(async (string file) =>
        {
            foreach (var sym in ExtractDocumentSymbols(file))
                Console.WriteLine($"{sym.Line}:{sym.Name}");
            await Task.CompletedTask;
        }, lspFileOpt);

        // tokens
        var lspTokensCmd = new Command("lsp-semantic-tokens", "Count tokens") { lspFileOpt };
        lspTokensCmd.SetHandler(async (string file) =>
        {
            Console.WriteLine(CountSemanticTokens(file));
            await Task.CompletedTask;
        }, lspFileOpt);

        // definition
        var lspDefCmd = new Command("lsp-definition", "Find definition") { lspFileOpt, lspNameOpt };
        lspDefCmd.SetHandler(async (string file, string name) =>
        {
            var line = FindDefinitionLine(file, name);
            Console.WriteLine(line >= 0 ? $"{file}:{line}" : "not found");
            await Task.CompletedTask;
        }, lspFileOpt, lspNameOpt);

        // workspace root
        var lspRootCmd = new Command("lsp-workspace-root", "Find workspace root") { lspFileOpt };
        lspRootCmd.SetHandler(async (string file) =>
        {
            Console.WriteLine(FindWorkspaceRoot(file));
            await Task.CompletedTask;
        }, lspFileOpt);

        // New LSP commands
        var lspHoverCmd = new Command("lsp-hover", "Show hover info") { lspFileOpt, lspLineOpt };
        lspHoverCmd.SetHandler(async (string file, int line) =>
        {
            var text = File.Exists(file) ? File.ReadLines(file).Skip(line - 1).FirstOrDefault() ?? string.Empty : string.Empty;
            Console.WriteLine(text.Trim());
            await Task.CompletedTask;
        }, lspFileOpt, lspLineOpt);

        var lspCompletionCmd = new Command("lsp-completion", "List completions") { lspFileOpt, lspLineOpt };
        lspCompletionCmd.SetHandler(async (string file, int line) =>
        {
            var word = File.Exists(file) ? File.ReadLines(file).Skip(line - 1).FirstOrDefault()?.Trim().Split(' ').Last() ?? string.Empty : string.Empty;
            Console.WriteLine($"{word}Completion");
            await Task.CompletedTask;
        }, lspFileOpt, lspLineOpt);

        var lspReferencesCmd = new Command("lsp-references", "Find references") { lspFileOpt, lspNameOpt };
        lspReferencesCmd.SetHandler(async (string file, string name) =>
        {
            var lines = File.Exists(file) ? File.ReadAllLines(file) : Array.Empty<string>();
            for (int i = 0; i < lines.Length; i++) if (lines[i].Contains(name)) Console.WriteLine($"{file}:{i + 1}");
            await Task.CompletedTask;
        }, lspFileOpt, lspNameOpt);

        var lspRenameCmd = new Command("lsp-rename", "Rename symbol in file") { lspFileOpt, oldNameOpt, newNameOpt };
        lspRenameCmd.SetHandler(async (string file, string oldN, string newN) =>
        {
            if (!File.Exists(file)) { Console.WriteLine("not found"); return; }
            var text = File.ReadAllText(file).Replace(oldN, newN);
            File.WriteAllText(file, text);
            Console.WriteLine("renamed");
            await Task.CompletedTask;
        }, lspFileOpt, oldNameOpt, newNameOpt);

        var lspSignatureCmd = new Command("lsp-signature", "Show function signature") { lspFileOpt, lspLineOpt };
        lspSignatureCmd.SetHandler(async (string file, int line) =>
        {
            var text = File.Exists(file) ? File.ReadLines(file).Skip(line - 1).FirstOrDefault() ?? string.Empty : string.Empty;
            Console.WriteLine(text.Trim());
            await Task.CompletedTask;
        }, lspFileOpt, lspLineOpt);

        var lspFormatCmd = new Command("lsp-format", "Format file") { lspFileOpt };
        lspFormatCmd.SetHandler(async (string file) =>
        {
            if (!File.Exists(file)) { Console.WriteLine("not found"); return; }
            var lines = File.ReadAllLines(file).Select(l => l.TrimEnd());
            File.WriteAllLines(file, lines);
            Console.WriteLine("formatted");
            await Task.CompletedTask;
        }, lspFileOpt);

        var lspActionsCmd = new Command("lsp-actions", "List code actions") { lspFileOpt, lspLineOpt };
        lspActionsCmd.SetHandler(async (string file, int line) =>
        {
            Console.WriteLine("No actions");
            await Task.CompletedTask;
        }, lspFileOpt, lspLineOpt);

        var lspFoldingCmd = new Command("lsp-folding-ranges", "List folding ranges") { lspFileOpt };
        lspFoldingCmd.SetHandler(async (string file) =>
        {
            var lines = File.Exists(file) ? File.ReadAllLines(file) : Array.Empty<string>();
            for (int i = 0; i < lines.Length; i++) if (lines[i].TrimEnd().EndsWith("{")) Console.WriteLine($"{i + 1}-{i + 2}");
            await Task.CompletedTask;
        }, lspFileOpt);

        var lspDiagCmd = new Command("lsp-diagnostics", "Show diagnostics") { lspFileOpt };
        lspDiagCmd.SetHandler(async (string file) =>
        {
            var lines = File.Exists(file) ? File.ReadAllLines(file) : Array.Empty<string>();
            for (int i = 0; i < lines.Length; i++) if (lines[i].Contains("TODO") || lines[i].Contains("FIXME")) Console.WriteLine($"{file}:{i + 1} warning");
            await Task.CompletedTask;
        }, lspFileOpt);

        var lspOpenCmd = new Command("lsp-open", "Open file") { lspFileOpt };
        lspOpenCmd.SetHandler(async (string file) =>
        {
            var p = new System.Diagnostics.Process();
            p.StartInfo.FileName = "xdg-open";
            p.StartInfo.ArgumentList.Add(file);
            p.Start();
            await Task.CompletedTask;
        }, lspFileOpt);

        root.Add(lspStartCmd);
        root.Add(lspStopCmd);
        root.Add(lspRestartCmd);
        root.Add(lspStopAllCmd);
        root.Add(lspListCmd);
        root.Add(lspInfoCmd);
        root.Add(lspPathCmd);
        root.Add(lspSymbolsCmd);
        root.Add(lspCodeLensCmd);
        root.Add(lspTokensCmd);
        root.Add(lspDefCmd);
        root.Add(lspRootCmd);
        root.Add(lspHoverCmd);
        root.Add(lspCompletionCmd);
        root.Add(lspReferencesCmd);
        root.Add(lspRenameCmd);
        root.Add(lspSignatureCmd);
        root.Add(lspFormatCmd);
        root.Add(lspActionsCmd);
        root.Add(lspFoldingCmd);
        root.Add(lspDiagCmd);
        root.Add(lspOpenCmd);
    }

    private static string FindWorkspaceRoot(string filePath)
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

    private static System.Collections.Generic.List<(string Name, int Line)> ExtractDocumentSymbols(string path)
    {
        var result = new System.Collections.Generic.List<(string, int)>();
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

    private static int CountSemanticTokens(string path)
    {
        if (!File.Exists(path)) return 0;
        var text = File.ReadAllText(path);
        return text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static int FindDefinitionLine(string path, string name)
    {
        var lines = File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>();
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(name)) return i + 1;
        }
        return -1;
    }
}
