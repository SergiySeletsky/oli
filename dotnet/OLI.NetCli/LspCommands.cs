using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.Json;

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

        var lspStopAllCmd = new Command("lsp-stop-all", "Stop all LSP servers");
        lspStopAllCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            int count = state.LspServers.Count;
            state.LspServers.Clear();
            Program.SaveState(state);
            Console.WriteLine($"Stopped {count}");
            await Task.CompletedTask;
        });

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

        var updateRootIdArg = new Argument<string>("id");
        var updateRootPathArg = new Argument<string>("path");
        var lspUpdateRootCmd = new Command("lsp-update-root", "Update server root path") { updateRootIdArg, updateRootPathArg };
        lspUpdateRootCmd.SetHandler(async (string id, string path) =>
        {
            var state = Program.LoadState();
            var info = state.LspServers.FirstOrDefault(s => s.Id == id);
            if (info == null) { Console.WriteLine("not found"); return; }
            info.RootPath = Path.GetFullPath(path);
            Program.SaveState(state);
            Console.WriteLine("updated");
            await Task.CompletedTask;
        }, updateRootIdArg, updateRootPathArg);

        var lspListCmd = new Command("lsp-list", "List LSP servers");
        lspListCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            if (state.LspServers.Count == 0) Console.WriteLine("No servers");
            foreach (var s in state.LspServers)
                Console.WriteLine($"{s.Id}: {s.Language} {s.RootPath}");
            await Task.CompletedTask;
        });

        var lspLanguageStatsCmd = new Command("lsp-language-stats", "Show count of servers per language");
        lspLanguageStatsCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var groups = state.LspServers.GroupBy(s => s.Language);
            foreach (var g in groups) Console.WriteLine($"{g.Key}:{g.Count()}");
            await Task.CompletedTask;
        });

        var exportLspArg = new Argument<string>("path");
        var exportLspCmd = new Command("export-lsp", "Export LSP servers to JSON") { exportLspArg };
        exportLspCmd.SetHandler(async (string path) =>
        {
            var state = Program.LoadState();
            File.WriteAllText(path, JsonSerializer.Serialize(state.LspServers, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"exported to {path}");
            await Task.CompletedTask;
        }, exportLspArg);

        var exportCsvArg = new Argument<string>("path");
        var lspExportCsvCmd = new Command("export-lsp-csv", "Export LSP servers to CSV") { exportCsvArg };
        lspExportCsvCmd.SetHandler(async (string path) =>
        {
            var state = Program.LoadState();
            using var writer = new StreamWriter(path);
            await writer.WriteLineAsync("id,language,root");
            foreach (var s in state.LspServers)
                await writer.WriteLineAsync($"{s.Id},{s.Language},{s.RootPath}");
            Console.WriteLine($"exported to {path}");
        }, exportCsvArg);

        var importLspArg = new Argument<string>("path");
        var importLspCmd = new Command("import-lsp", "Import LSP servers from JSON") { importLspArg };
        importLspCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("file not found"); return; }
            var json = File.ReadAllText(path);
            var list = JsonSerializer.Deserialize<List<LspServerInfo>>(json);
            if (list != null)
            {
                var state = Program.LoadState();
                state.LspServers = list;
                Program.SaveState(state);
                Console.WriteLine("imported");
            }
            else Console.WriteLine("invalid file");
            await Task.CompletedTask;
        }, importLspArg);

        var importCsvArg = new Argument<string>("path");
        var lspImportCsvCmd = new Command("import-lsp-csv", "Import LSP servers from CSV") { importCsvArg };
        lspImportCsvCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("file not found"); return; }
            var lines = File.ReadAllLines(path).Skip(1);
            var list = new List<LspServerInfo>();
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length < 3) continue;
                list.Add(new LspServerInfo { Id = parts[0], Language = parts[1], RootPath = parts[2] });
            }
            var state = Program.LoadState();
            state.LspServers = list;
            Program.SaveState(state);
            Console.WriteLine("imported");
            await Task.CompletedTask;
        }, importCsvArg);

        var infoIdArg = new Argument<string>("id");
        var lspInfoCmd = new Command("lsp-info", "Show details for an LSP server") { infoIdArg };
        lspInfoCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var info = state.LspServers.FirstOrDefault(s => s.Id == id);
            if (info != null)
                Console.WriteLine($"{info.Id}: {info.Language} {info.RootPath}");
            else
                Console.WriteLine("not found");
            await Task.CompletedTask;
        }, infoIdArg);

        var openRootIdArg = new Argument<string>("id");
        var lspOpenRootCmd = new Command("lsp-open-root", "Open the root path in file explorer") { openRootIdArg };
        lspOpenRootCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var info = state.LspServers.FirstOrDefault(s => s.Id == id);
            if (info == null || !Directory.Exists(info.RootPath)) { Console.WriteLine("not found"); return; }
            System.Diagnostics.Process.Start("xdg-open", info.RootPath);
            await Task.CompletedTask;
        }, openRootIdArg);

        var setLangIdArg = new Argument<string>("id");
        var setLangArg = new Argument<string>("language");
        var lspSetLangCmd = new Command("lsp-set-language", "Update server language") { setLangIdArg, setLangArg };
        lspSetLangCmd.SetHandler(async (string id, string language) =>
        {
            var state = Program.LoadState();
            var info = state.LspServers.FirstOrDefault(s => s.Id == id);
            if (info == null) { Console.WriteLine("not found"); return; }
            info.Language = language;
            Program.SaveState(state);
            Console.WriteLine("updated");
            await Task.CompletedTask;
        }, setLangIdArg, setLangArg);

        var lspCountCmd = new Command("lsp-count", "Show number of LSP servers");
        lspCountCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            Console.WriteLine(state.LspServers.Count);
            await Task.CompletedTask;
        });

        var langSearchArg = new Argument<string>("language");
        var lspFindLangCmd = new Command("lsp-find-language", "List servers by language") { langSearchArg };
        lspFindLangCmd.SetHandler(async (string language) =>
        {
            var state = Program.LoadState();
            foreach (var s in state.LspServers.Where(s => s.Language.Equals(language, StringComparison.OrdinalIgnoreCase)))
                Console.WriteLine(s.Id);
            await Task.CompletedTask;
        }, langSearchArg);

        var rootSearchArg = new Argument<string>("path");
        var lspFindRootCmd = new Command("lsp-find-root", "List servers with root path containing text") { rootSearchArg };
        lspFindRootCmd.SetHandler(async (string path) =>
        {
            var state = Program.LoadState();
            foreach (var s in state.LspServers.Where(s => s.RootPath.Contains(path, StringComparison.OrdinalIgnoreCase)))
                Console.WriteLine(s.Id);
            await Task.CompletedTask;
        }, rootSearchArg);

        var lspPathCmd = new Command("lsp-path", "Show path to LSP server list");
        lspPathCmd.SetHandler(() => { Console.WriteLine(Program.LspPath); });

        var hoverIdArg = new Argument<string>("id");
        var hoverFileArg = new Argument<string>("file");
        var hoverLineArg = new Argument<int>("line");
        var hoverColArg = new Argument<int>("col");
        var lspHoverCmd = new Command("lsp-hover", "Get hover info") { hoverIdArg, hoverFileArg, hoverLineArg, hoverColArg };
        lspHoverCmd.SetHandler(async (string id, string file, int line, int col) =>
        {
            Console.WriteLine("hover not implemented");
            await Task.CompletedTask;
        }, hoverIdArg, hoverFileArg, hoverLineArg, hoverColArg);

        var compIdArg = new Argument<string>("id");
        var compFileArg = new Argument<string>("file");
        var compLineArg = new Argument<int>("line");
        var compColArg = new Argument<int>("col");
        var lspCompletionCmd = new Command("lsp-completion", "Get completion items") { compIdArg, compFileArg, compLineArg, compColArg };
        lspCompletionCmd.SetHandler(async (string id, string file, int line, int col) =>
        {
            Console.WriteLine("completion not implemented");
            await Task.CompletedTask;
        }, compIdArg, compFileArg, compLineArg, compColArg);

        var refIdArg = new Argument<string>("id");
        var refFileArg = new Argument<string>("file");
        var refLineArg = new Argument<int>("line");
        var refColArg = new Argument<int>("col");
        var lspReferencesCmd = new Command("lsp-references", "Find references") { refIdArg, refFileArg, refLineArg, refColArg };
        lspReferencesCmd.SetHandler(async (string id, string file, int line, int col) =>
        {
            Console.WriteLine("references not implemented");
            await Task.CompletedTask;
        }, refIdArg, refFileArg, refLineArg, refColArg);

        var renameIdArg = new Argument<string>("id");
        var renameFileArg = new Argument<string>("file");
        var renameLineArg = new Argument<int>("line");
        var renameColArg = new Argument<int>("col");
        var renameNameArg = new Argument<string>("new-name");
        var lspRenameCmd = new Command("lsp-rename", "Rename symbol") { renameIdArg, renameFileArg, renameLineArg, renameColArg, renameNameArg };
        lspRenameCmd.SetHandler(async (string id, string file, int line, int col, string newName) =>
        {
            Console.WriteLine("rename not implemented");
            await Task.CompletedTask;
        }, renameIdArg, renameFileArg, renameLineArg, renameColArg, renameNameArg);

        var sigIdArg = new Argument<string>("id");
        var sigFileArg = new Argument<string>("file");
        var sigLineArg = new Argument<int>("line");
        var sigColArg = new Argument<int>("col");
        var lspSignatureCmd = new Command("lsp-signature", "Signature help") { sigIdArg, sigFileArg, sigLineArg, sigColArg };
        lspSignatureCmd.SetHandler(async (string id, string file, int line, int col) =>
        {
            Console.WriteLine("signature not implemented");
            await Task.CompletedTask;
        }, sigIdArg, sigFileArg, sigLineArg, sigColArg);

        var fmtIdArg = new Argument<string>("id");
        var fmtFileArg = new Argument<string>("file");
        var lspFormatCmd = new Command("lsp-format", "Format document") { fmtIdArg, fmtFileArg };
        lspFormatCmd.SetHandler(async (string id, string file) =>
        {
            Console.WriteLine("format not implemented");
            await Task.CompletedTask;
        }, fmtIdArg, fmtFileArg);

        var actionsIdArg = new Argument<string>("id");
        var lspActionsCmd = new Command("lsp-actions", "List code actions") { actionsIdArg };
        lspActionsCmd.SetHandler(async (string id) =>
        {
            Console.WriteLine("actions not implemented");
            await Task.CompletedTask;
        }, actionsIdArg);

        var foldIdArg = new Argument<string>("id");
        var foldFileArg = new Argument<string>("file");
        var lspFoldCmd = new Command("lsp-folding-ranges", "Get folding ranges") { foldIdArg, foldFileArg };
        lspFoldCmd.SetHandler(async (string id, string file) =>
        {
            Console.WriteLine("folding not implemented");
            await Task.CompletedTask;
        }, foldIdArg, foldFileArg);

        var diagIdArg = new Argument<string>("id");
        var lspDiagCmd = new Command("lsp-diagnostics", "Show diagnostics") { diagIdArg };
        lspDiagCmd.SetHandler(async (string id) =>
        {
            Console.WriteLine("diagnostics not implemented");
            await Task.CompletedTask;
        }, diagIdArg);

        var openIdArg = new Argument<string>("id");
        var lspOpenCmd = new Command("lsp-open", "Open server root") { openIdArg };
        lspOpenCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var info = state.LspServers.FirstOrDefault(s => s.Id == id);
            if (info == null || !Directory.Exists(info.RootPath)) { Console.WriteLine("not found"); return; }
            Process.Start("xdg-open", info.RootPath);
            await Task.CompletedTask;
        }, openIdArg);

        root.Add(lspStartCmd);
        root.Add(lspStopCmd);
        root.Add(lspStopAllCmd);
        root.Add(lspRestartCmd);
        root.Add(lspUpdateRootCmd);
        root.Add(lspListCmd);
        root.Add(lspLanguageStatsCmd);
        root.Add(exportLspCmd);
        root.Add(lspExportCsvCmd);
        root.Add(importLspCmd);
        root.Add(lspImportCsvCmd);
        root.Add(lspInfoCmd);
        root.Add(lspOpenRootCmd);
        root.Add(lspSetLangCmd);
        root.Add(lspCountCmd);
        root.Add(lspFindLangCmd);
        root.Add(lspFindRootCmd);
        root.Add(lspHoverCmd);
        root.Add(lspCompletionCmd);
        root.Add(lspReferencesCmd);
        root.Add(lspRenameCmd);
        root.Add(lspSignatureCmd);
        root.Add(lspFormatCmd);
        root.Add(lspActionsCmd);
        root.Add(lspFoldCmd);
        root.Add(lspDiagCmd);
        root.Add(lspOpenCmd);
        root.Add(lspPathCmd);
    }
}
