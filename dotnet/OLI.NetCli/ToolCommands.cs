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
        var statusOpt = new Option<string>("--status") { IsRequired = true };
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

        var toolFailureCountCmd = new Command("tool-failure-count", "Number of failed tool runs");
        toolFailureCountCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            Console.WriteLine(state.ToolExecutions.Count(t => t.Status == "error"));
            await Task.CompletedTask;
        });

        var toolSuccessRateCmd = new Command("tool-success-rate", "Percentage of successful tool runs");
        toolSuccessRateCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            if (state.ToolExecutions.Count == 0) { Console.WriteLine("0"); return; }
            int success = state.ToolExecutions.Count(t => t.Status == "success");
            double rate = success * 100.0 / state.ToolExecutions.Count;
            Console.WriteLine(rate.ToString("F2"));
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

        var toolProgressCmd = new Command("tool-progress", "Show tool progress") { toolIdOpt };
        toolProgressCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var tool = state.ToolExecutions.FirstOrDefault(t => t.Id == id);
            Console.WriteLine(tool?.Message ?? "not found");
            await Task.CompletedTask;
        }, toolIdOpt);

        var toolProgressAllCmd = new Command("tool-progress-all", "Show progress for all tools");
        toolProgressAllCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var t in state.ToolExecutions)
                Console.WriteLine($"{t.Id}: {t.Message}");
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

        var listToolIdsCmd = new Command("list-tool-ids", "List tool execution IDs");
        listToolIdsCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var t in state.ToolExecutions) Console.WriteLine(t.Id);
            await Task.CompletedTask;
        });

        var toolExistsCmd = new Command("tool-exists", "Check if tool id exists") { toolIdOpt };
        toolExistsCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            Console.WriteLine(state.ToolExecutions.Any(t => t.Id == id) ? "true" : "false");
            await Task.CompletedTask;
        }, toolIdOpt);

        var latestToolCmd = new Command("latest-tool", "Show most recent tool id");
        latestToolCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var tool = state.ToolExecutions.OrderByDescending(t => t.StartTime).FirstOrDefault();
            Console.WriteLine(tool?.Id ?? "none");
            await Task.CompletedTask;
        });

        var toolDurationCmd = new Command("tool-duration", "Show tool run duration") { toolIdOpt };
        toolDurationCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var tool = state.ToolExecutions.FirstOrDefault(t => t.Id == id);
            if (tool?.EndTime != null) Console.WriteLine((tool.EndTime.Value - tool.StartTime).TotalSeconds);
            else Console.WriteLine("running");
            await Task.CompletedTask;
        }, toolIdOpt);

        var toolsByStatusCmd = new Command("tools-by-status", "List tools with status") { statusOpt };
        toolsByStatusCmd.SetHandler(async (string status) =>
        {
            var state = Program.LoadState();
            foreach (var t in state.ToolExecutions.Where(te => te.Status == status))
                Console.WriteLine(t.Id);
            await Task.CompletedTask;
        }, statusOpt);

        var toolAgeCmd = new Command("tool-age", "Show seconds since tool start") { toolIdOpt };
        toolAgeCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var tool = state.ToolExecutions.FirstOrDefault(t => t.Id == id);
            if (tool != null) Console.WriteLine((DateTime.UtcNow - tool.StartTime).TotalSeconds);
            else Console.WriteLine("not found");
            await Task.CompletedTask;
        }, toolIdOpt);

        var recentOpt = new Option<int>("--minutes", () => 60);
        var toolsRecentCmd = new Command("tools-recent", "List tools started in last minutes") { recentOpt };
        toolsRecentCmd.SetHandler(async (int minutes) =>
        {
            var state = Program.LoadState();
            var cutoff = DateTime.UtcNow.AddMinutes(-minutes);
            foreach (var t in state.ToolExecutions.Where(te => te.StartTime >= cutoff))
                Console.WriteLine(t.Id);
            await Task.CompletedTask;
        }, recentOpt);

        var runningToolCountCmd = new Command("running-tool-count", "Number of running tools");
        runningToolCountCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            Console.WriteLine(state.ToolExecutions.Count(te => te.Status == "running"));
            await Task.CompletedTask;
        });

        var nameArg = new Argument<string>("name");
        var toolsByNameCmd = new Command("tools-by-name", "List tools by name") { nameArg };
        toolsByNameCmd.SetHandler(async (string name) =>
        {
            var state = Program.LoadState();
            foreach (var t in state.ToolExecutions.Where(te => te.Name == name)) Console.WriteLine(t.Id);
            await Task.CompletedTask;
        }, nameArg);

        var toolCountByNameCmd = new Command("tool-count-by-name", "Count tools by name") { nameArg };
        toolCountByNameCmd.SetHandler(async (string name) =>
        {
            var state = Program.LoadState();
            Console.WriteLine(state.ToolExecutions.Count(te => te.Name == name));
            await Task.CompletedTask;
        }, nameArg);

        var exportRunArg = new Argument<string>("path");
        var exportRunCmd = new Command("export-tool-run", "Export a tool by id") { toolIdOpt, exportRunArg };
        exportRunCmd.SetHandler(async (string id, string path) =>
        {
            var state = Program.LoadState();
            var tool = state.ToolExecutions.FirstOrDefault(t => t.Id == id);
            if (tool != null)
            {
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(tool, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine("exported");
            }
            else Console.WriteLine("not found");
        }, toolIdOpt, exportRunArg);

        var importRunArg = new Argument<string>("path");
        var importRunCmd = new Command("import-tool-run", "Import a tool run") { importRunArg };
        importRunCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("file not found"); return; }
            var json = File.ReadAllText(path);
            var tool = JsonSerializer.Deserialize<ToolExecution>(json);
            if (tool != null)
            {
                var state = Program.LoadState();
                state.ToolExecutions.Add(tool);
                Program.SaveState(state);
                Console.WriteLine("imported");
            }
            else Console.WriteLine("invalid");
            await Task.CompletedTask;
        }, importRunArg);

        var clearToolsCmd = new Command("clear-tools", "Remove all tool executions");
        clearToolsCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            state.ToolExecutions.Clear();
            Program.SaveState(state);
            Console.WriteLine("cleared");
            await Task.CompletedTask;
        });

        root.AddCommand(startToolCmd);
        root.AddCommand(updateToolCmd);
        root.AddCommand(completeToolCmd);
        root.AddCommand(failToolCmd);
        root.AddCommand(cleanupToolsCmd);
        root.AddCommand(listToolsCmd);
        root.AddCommand(toolInfoCmd);
        root.AddCommand(toolCountCmd);
        root.AddCommand(toolFailureCountCmd);
        root.AddCommand(toolSuccessRateCmd);
        root.AddCommand(runningToolsCmd);
        root.AddCommand(toolProgressCmd);
        root.AddCommand(toolProgressAllCmd);
        root.AddCommand(listToolIdsCmd);
        root.AddCommand(toolExistsCmd);
        root.AddCommand(latestToolCmd);
        root.AddCommand(toolDurationCmd);
        root.AddCommand(toolsByStatusCmd);
        root.AddCommand(toolAgeCmd);
        root.AddCommand(toolsRecentCmd);
        root.AddCommand(runningToolCountCmd);
        root.AddCommand(toolsByNameCmd);
        root.AddCommand(toolCountByNameCmd);
        root.AddCommand(exportRunCmd);
        root.AddCommand(importRunCmd);
        root.AddCommand(clearToolsCmd);
        root.AddCommand(listToolsByTaskCmd);
        root.AddCommand(deleteToolCmd);
        root.AddCommand(setToolMetaCmd);
        root.AddCommand(exportToolsCmd);
        root.AddCommand(importToolsCmd);
    }
}
