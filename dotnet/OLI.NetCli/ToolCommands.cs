using System;
using System.CommandLine;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

public static class ToolCommands
{
    public static void Register(RootCommand root)
    {
        var startToolTaskOpt = new Option<string>("--task-id") { IsRequired = true };
        var startToolNameOpt = new Option<string>("--name") { IsRequired = true };
        var startToolCmd = new Command("start-tool", "Begin tool execution")
        {
            startToolTaskOpt, startToolNameOpt
        };
        startToolCmd.SetHandler(async (string taskId, string name) =>
        {
            var state = Program.LoadState();
            var exec = new ToolExecution
            {
                TaskId = taskId,
                Name = name,
                Message = "starting"
            };
            state.ToolExecutions.Add(exec);
            Program.SaveState(state);
            Console.WriteLine(exec.Id);
            await Task.CompletedTask;
        }, startToolTaskOpt, startToolNameOpt);

        var toolIdOpt = new Option<string>("--id") { IsRequired = true };
        var msgOpt = new Option<string>("--message") { IsRequired = true };
        var updateToolCmd = new Command("update-tool-progress", "Update tool message")
        {
            toolIdOpt, msgOpt
        };
        updateToolCmd.SetHandler(async (string id, string message) =>
        {
            var state = Program.LoadState();
            var tool = state.ToolExecutions.Find(t => t.Id == id);
            if (tool != null)
            {
                tool.Message = message;
                Program.SaveState(state);
                Console.WriteLine("updated");
            }
            else
            {
                Console.WriteLine("Tool not found");
            }
            await Task.CompletedTask;
        }, toolIdOpt, msgOpt);

        var completeToolCmd = new Command("complete-tool", "Finish tool execution")
        {
            toolIdOpt, msgOpt
        };
        completeToolCmd.SetHandler(async (string id, string message) =>
        {
            var state = Program.LoadState();
            var tool = state.ToolExecutions.Find(t => t.Id == id);
            if (tool != null)
            {
                tool.Message = message;
                tool.Status = "success";
                tool.EndTime = DateTime.UtcNow;
                Program.SaveState(state);
                Console.WriteLine("completed");
            }
            else
            {
                Console.WriteLine("Tool not found");
            }
            await Task.CompletedTask;
        }, toolIdOpt, msgOpt);

        var failToolCmd = new Command("fail-tool", "Mark tool execution failed")
        {
            toolIdOpt, msgOpt
        };
        failToolCmd.SetHandler(async (string id, string message) =>
        {
            var state = Program.LoadState();
            var tool = state.ToolExecutions.Find(t => t.Id == id);
            if (tool != null)
            {
                tool.Message = message;
                tool.Status = "error";
                tool.EndTime = DateTime.UtcNow;
                Program.SaveState(state);
                Console.WriteLine("failed");
            }
            else
            {
                Console.WriteLine("Tool not found");
            }
            await Task.CompletedTask;
        }, toolIdOpt, msgOpt);

        var cleanupToolsCmd = new Command("cleanup-tools", "Remove old tool executions");
        cleanupToolsCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var cutoff = DateTime.UtcNow.AddMinutes(-10);
            state.ToolExecutions.RemoveAll(t => t.EndTime.HasValue && t.EndTime < cutoff);
            Program.SaveState(state);
            Console.WriteLine("cleaned");
            await Task.CompletedTask;
        });

        var listToolsCmd = new Command("list-tools", "List tool executions");
        listToolsCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var t in state.ToolExecutions)
            {
                Console.WriteLine($"{t.Id}: {t.Name} [{t.Status}] {t.Message}");
            }
            await Task.CompletedTask;
        });

        var toolInfoCmd = new Command("tool-info", "Show tool details") { toolIdOpt };
        toolInfoCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
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
        }, toolIdOpt);

        var toolCountCmd = new Command("tool-count", "Number of tool executions");
        toolCountCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            Console.WriteLine(state.ToolExecutions.Count);
            await Task.CompletedTask;
        });

        var runningToolsCmd = new Command("running-tools", "List running tools");
        runningToolsCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var t in state.ToolExecutions.Where(te => te.Status == "running"))
            {
                Console.WriteLine($"{t.Id}: {t.Name} {t.Message}");
            }
            await Task.CompletedTask;
        });

        var listToolsByTaskCmd = new Command("list-tools-by-task", "List tools for a task") { startToolTaskOpt };
        listToolsByTaskCmd.SetHandler(async (string taskId) =>
        {
            var state = Program.LoadState();
            foreach (var t in state.ToolExecutions.Where(te => te.TaskId == taskId))
            {
                Console.WriteLine($"{t.Id}: {t.Name} [{t.Status}] {t.Message}");
            }
            await Task.CompletedTask;
        }, startToolTaskOpt);

        var deleteToolCmd = new Command("delete-tool", "Remove a tool execution") { toolIdOpt };
        deleteToolCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var removed = state.ToolExecutions.RemoveAll(te => te.Id == id);
            Program.SaveState(state);
            Console.WriteLine(removed > 0 ? "deleted" : "not found");
            await Task.CompletedTask;
        }, toolIdOpt);

        var metaKeyOpt = new Option<string>("--key") { IsRequired = true };
        var metaValOpt = new Option<string>("--value") { IsRequired = true };
        var setToolMetaCmd = new Command("set-tool-metadata", "Set metadata on tool") { toolIdOpt, metaKeyOpt, metaValOpt };
        setToolMetaCmd.SetHandler(async (string id, string key, string value) =>
        {
            var state = Program.LoadState();
            var tool = state.ToolExecutions.Find(t => t.Id == id);
            if (tool != null)
            {
                tool.Metadata ??= new Dictionary<string, object>();
                tool.Metadata[key] = value;
                Program.SaveState(state);
                Console.WriteLine("metadata set");
            }
            else
            {
                Console.WriteLine("Tool not found");
            }
            await Task.CompletedTask;
        }, toolIdOpt, metaKeyOpt, metaValOpt);

        var exportToolsPathOpt = new Option<string>("--path") { IsRequired = true };
        var exportToolsCmd = new Command("export-tools", "Save tools to JSON") { exportToolsPathOpt };
        exportToolsCmd.SetHandler(async (string path) =>
        {
            var state = Program.LoadState();
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
                var state = Program.LoadState();
                state.ToolExecutions = tools;
                Program.SaveState(state);
                Console.WriteLine("Tools imported");
            }
            else
            {
                Console.WriteLine("Invalid tools file");
            }
            await Task.CompletedTask;
        }, importToolsPathOpt);

        root.AddCommand(startToolCmd);
        root.AddCommand(updateToolCmd);
        root.AddCommand(completeToolCmd);
        root.AddCommand(failToolCmd);
        root.AddCommand(cleanupToolsCmd);
        root.AddCommand(listToolsCmd);
        root.AddCommand(toolInfoCmd);
        root.AddCommand(toolCountCmd);
        root.AddCommand(runningToolsCmd);
        root.AddCommand(listToolsByTaskCmd);
        root.AddCommand(deleteToolCmd);
        root.AddCommand(setToolMetaCmd);
        root.AddCommand(exportToolsCmd);
        root.AddCommand(importToolsCmd);
    }
}
