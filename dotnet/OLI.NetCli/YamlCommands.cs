using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

public static class YamlCommands
{
    public static void Register(RootCommand root)
    {
        var yamlArg = new Argument<string>("path");

        var convToYamlCmd = new Command("conversation-to-yaml", "Export conversation to YAML") { yamlArg };
        convToYamlCmd.SetHandler(async (string path) =>
        {
            var state = Program.LoadState();
            YamlUtils.Write(path, state.Conversation);
            await Task.CompletedTask;
        }, yamlArg);

        var convFromYamlCmd = new Command("conversation-from-yaml", "Import conversation from YAML") { yamlArg };
        convFromYamlCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("file not found"); return; }
            var list = YamlUtils.Read<List<string>>(path);
            var state = Program.LoadState();
            state.Conversation = list ?? new List<string>();
            Program.SaveState(state);
            Console.WriteLine("loaded");
            await Task.CompletedTask;
        }, yamlArg);

        var memToYamlCmd = new Command("memory-to-yaml", "Export memory file to YAML") { yamlArg };
        memToYamlCmd.SetHandler(async (string path) =>
        {
            var lines = File.Exists(Program.MemoryPath) ? File.ReadAllLines(Program.MemoryPath) : Array.Empty<string>();
            YamlUtils.Write(path, lines);
            await Task.CompletedTask;
        }, yamlArg);

        var memFromYamlCmd = new Command("memory-from-yaml", "Import memory from YAML") { yamlArg };
        memFromYamlCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("file not found"); return; }
            var lines = YamlUtils.Read<List<string>>(path);
            File.WriteAllLines(Program.MemoryPath, lines?.ToArray() ?? Array.Empty<string>());
            Console.WriteLine("loaded");
            await Task.CompletedTask;
        }, yamlArg);

        var tasksToYamlCmd = new Command("tasks-to-yaml", "Export tasks to YAML") { yamlArg };
        tasksToYamlCmd.SetHandler(async (string path) =>
        {
            var state = Program.LoadState();
            YamlUtils.Write(path, state.Tasks);
            await Task.CompletedTask;
        }, yamlArg);

        var tasksFromYamlCmd = new Command("tasks-from-yaml", "Import tasks from YAML") { yamlArg };
        tasksFromYamlCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("file not found"); return; }
            var tasks = YamlUtils.Read<List<TaskRecord>>(path);
            var state = Program.LoadState();
            state.Tasks = tasks ?? new List<TaskRecord>();
            Program.SaveState(state);
            Console.WriteLine("loaded");
            await Task.CompletedTask;
        }, yamlArg);

        var lspToYamlCmd = new Command("lsp-to-yaml", "Export LSP servers to YAML") { yamlArg };
        lspToYamlCmd.SetHandler(async (string path) =>
        {
            var state = Program.LoadState();
            YamlUtils.Write(path, state.LspServers);
            await Task.CompletedTask;
        }, yamlArg);

        var lspFromYamlCmd = new Command("lsp-from-yaml", "Import LSP servers from YAML") { yamlArg };
        lspFromYamlCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("file not found"); return; }
            var servers = YamlUtils.Read<List<LspServerInfo>>(path);
            var state = Program.LoadState();
            state.LspServers = servers ?? new List<LspServerInfo>();
            Program.SaveState(state);
            Console.WriteLine("loaded");
            await Task.CompletedTask;
        }, yamlArg);

        var exportStateYamlCmd = new Command("export-state-yaml", "Export state to YAML") { yamlArg };
        exportStateYamlCmd.SetHandler(async (string path) =>
        {
            var state = Program.LoadState();
            YamlUtils.Write(path, state);
            await Task.CompletedTask;
        }, yamlArg);

        var importStateYamlCmd = new Command("import-state-yaml", "Import state from YAML") { yamlArg };
        importStateYamlCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("file not found"); return; }
            var state = YamlUtils.Read<AppState>(path);
            Program.SaveState(state);
            Console.WriteLine("imported");
            await Task.CompletedTask;
        }, yamlArg);

        root.Add(convToYamlCmd);
        root.Add(convFromYamlCmd);
        root.Add(memToYamlCmd);
        root.Add(memFromYamlCmd);
        root.Add(tasksToYamlCmd);
        root.Add(tasksFromYamlCmd);
        root.Add(lspToYamlCmd);
        root.Add(lspFromYamlCmd);
        root.Add(exportStateYamlCmd);
        root.Add(importStateYamlCmd);
    }
}
