using System;
using System.CommandLine;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using static MemoryUtils;

public static class TaskCommands
{
    public static void Register(RootCommand root)
    {
        var tasksCmd = new Command("tasks", "List tasks");
        tasksCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            if (state.Tasks.Count == 0)
            {
                Console.WriteLine("No tasks.");
            }
            foreach (var t in state.Tasks)
            {
                var duration = (t.UpdatedAt - t.CreatedAt).TotalSeconds;
                var due = t.DueDate != null ? $" Due:{t.DueDate:u}" : "";
                var tags = t.Tags.Count > 0 ? $" Tags:{string.Join(',', t.Tags)}" : "";
                Console.WriteLine($"{t.Id}: {t.Description} [{t.Status}] Tools:{t.ToolCount} Tokens:{t.InputTokens}/{t.OutputTokens} Duration:{duration:F0}s{due}{tags}");
            }
            await Task.CompletedTask;
        });

        var descriptionOption = new Option<string>("--description") { IsRequired = true };
        var dueOption = new Option<string?>("--due");
        var tagsOption = new Option<string?>("--tags", "comma separated tags");
        var createTaskCmd = new Command("create-task", "Add a new task")
        {
            descriptionOption, dueOption, tagsOption
        };
        createTaskCmd.SetHandler(async (string description, string? due, string? tags) =>
        {
            var state = Program.LoadState();
            var task = new TaskRecord
            {
                Description = description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            if (due != null && DateTime.TryParse(due, out var dt)) task.DueDate = dt;
            if (!string.IsNullOrEmpty(tags)) task.Tags = tags.Split(',').Select(t => t.Trim()).Where(t => t.Length>0).ToList();
            state.Tasks.Add(task);
            state.CurrentTaskId = task.Id;
            Program.SaveState(state);
            Console.WriteLine($"Created task {task.Id}");
            await Task.CompletedTask;
        }, descriptionOption, dueOption, tagsOption);

        var completeIdOption = new Option<string>("--id") { IsRequired = true };
        var outputTokensOption = new Option<int>("--output-tokens", () => 0);
        var completeTaskCmd = new Command("complete-task", "Mark a task as completed")
        {
            completeIdOption, outputTokensOption
        };
        completeTaskCmd.SetHandler(async (string id, int outputTokens) =>
        {
            var state = Program.LoadState();
            var task = state.Tasks.Find(t => t.Id == id);
            if (task != null)
            {
                task.Status = "completed";
                task.OutputTokens = outputTokens;
                task.UpdatedAt = DateTime.UtcNow;
                state.CurrentTaskId = null;
                Program.SaveState(state);
                Console.WriteLine("Task completed");
            }
            else
            {
                Console.WriteLine("Task not found");
            }
            await Task.CompletedTask;
        }, completeIdOption, outputTokensOption);

        var failIdOpt = new Option<string>("--id") { IsRequired = true };
        var errorOpt = new Option<string>("--error") { IsRequired = true };
        var failTaskCmd = new Command("fail-task", "Mark a task as failed")
        {
            failIdOpt, errorOpt
        };
        failTaskCmd.SetHandler(async (string id, string error) =>
        {
            var state = Program.LoadState();
            var task = state.Tasks.Find(t => t.Id == id);
            if (task != null)
            {
                task.Status = $"failed:{error}";
                task.UpdatedAt = DateTime.UtcNow;
                state.CurrentTaskId = null;
                Program.SaveState(state);
                Console.WriteLine("Task failed");
            }
            else
            {
                Console.WriteLine("Task not found");
            }
            await Task.CompletedTask;
        }, failIdOpt, errorOpt);

        var currentTaskCmd = new Command("current-task", "Show current task id");
        currentTaskCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            Console.WriteLine(state.CurrentTaskId ?? "none");
            await Task.CompletedTask;
        });

        var setCurrentIdOpt = new Option<string>("--id") { IsRequired = true };
        var setCurrentTaskCmd = new Command("set-current-task", "Set current task id")
        {
            setCurrentIdOpt
        };
        setCurrentTaskCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            if (state.Tasks.Any(t => t.Id == id))
            {
                state.CurrentTaskId = id;
                Program.SaveState(state);
                Console.WriteLine($"Current task set to {id}");
            }
            else
            {
                Console.WriteLine("Task not found");
            }
            await Task.CompletedTask;
        }, setCurrentIdOpt);

        var cancelIdOption = new Option<string>("--id", () => string.Empty, "Task id to cancel");
        var cancelTaskCmd = new Command("cancel-task", "Cancel a task by id")
        {
            cancelIdOption
        };
        cancelTaskCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
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
            Program.SaveState(state);
            Console.WriteLine("Task canceled");
            await Task.CompletedTask;
        }, cancelIdOption);

        var statsIdOpt = new Option<string>("--id") { IsRequired = true };
        var taskStatsCmd = new Command("task-stats", "Show task details")
        {
            statsIdOpt
        };
        taskStatsCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var task = state.Tasks.Find(t => t.Id == id);
            if (task != null)
            {
                var duration = (task.UpdatedAt - task.CreatedAt).TotalSeconds;
                Console.WriteLine($"Id: {task.Id}\nDesc: {task.Description}\nStatus: {task.Status}\nTools: {task.ToolCount}\nInput Tokens: {task.InputTokens}\nOutput Tokens: {task.OutputTokens}\nDuration: {duration:F0}s");
            }
            else
            {
                Console.WriteLine("Task not found");
            }
            await Task.CompletedTask;
        }, statsIdOpt);

        var addTokensIdOpt = new Option<string>("--id") { IsRequired = true };
        var tokensOpt = new Option<int>("--tokens") { IsRequired = true };
        var addInputTokensCmd = new Command("add-input-tokens", "Add tokens to a task")
        {
            addTokensIdOpt, tokensOpt
        };
        addInputTokensCmd.SetHandler(async (string id, int tokens) =>
        {
            var state = Program.LoadState();
            var task = state.Tasks.Find(t => t.Id == id);
            if (task != null)
            {
                task.InputTokens += tokens;
                task.UpdatedAt = DateTime.UtcNow;
                Program.SaveState(state);
                Console.WriteLine($"Added {tokens} tokens");
            }
            else
            {
                Console.WriteLine("Task not found");
            }
            await Task.CompletedTask;
        }, addTokensIdOpt, tokensOpt);

        var addOutputTokensCmd = new Command("add-output-tokens", "Increment output tokens")
        {
            addTokensIdOpt, tokensOpt
        };
        addOutputTokensCmd.SetHandler(async (string id, int tokens) =>
        {
            var state = Program.LoadState();
            var task = state.Tasks.Find(t => t.Id == id);
            if (task != null)
            {
                task.OutputTokens += tokens;
                task.UpdatedAt = DateTime.UtcNow;
                Program.SaveState(state);
                Console.WriteLine($"Added {tokens} output tokens");
            }
            else
            {
                Console.WriteLine("Task not found");
            }
            await Task.CompletedTask;
        }, addTokensIdOpt, tokensOpt);

        var taskDurationCmd = new Command("task-duration", "Show task duration") { statsIdOpt };
        taskDurationCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var task = state.Tasks.Find(t => t.Id == id);
            if (task != null)
            {
                var duration = (task.UpdatedAt - task.CreatedAt).TotalSeconds;
                Console.WriteLine(duration);
            }
            else
            {
                Console.WriteLine("Task not found");
            }
            await Task.CompletedTask;
        }, statsIdOpt);

        var addToolUseCmd = new Command("add-tool-use", "Increment tool count")
        {
            addTokensIdOpt
        };
        addToolUseCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var task = state.Tasks.Find(t => t.Id == id);
            if (task != null)
            {
                task.ToolCount += 1;
                task.UpdatedAt = DateTime.UtcNow;
                Program.SaveState(state);
                Console.WriteLine("Tool count updated");
            }
            else
            {
                Console.WriteLine("Task not found");
            }
            await Task.CompletedTask;
        }, addTokensIdOpt);

        var taskCountCmd = new Command("task-count", "Show number of tasks");
        taskCountCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            Console.WriteLine(state.Tasks.Count);
            await Task.CompletedTask;
        });

        var clearTasksCmd = new Command("clear-tasks", "Remove all tasks");
        clearTasksCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            state.Tasks.Clear();
            Program.SaveState(state);
            Console.WriteLine("Tasks cleared");
            await Task.CompletedTask;
        });

        var clearCompletedCmd = new Command("clear-completed-tasks", "Remove completed tasks");
        clearCompletedCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            state.Tasks.RemoveAll(t => t.Status == "completed");
            Program.SaveState(state);
            Console.WriteLine("Completed tasks removed");
            await Task.CompletedTask;
        });

        var statusOpt = new Option<string>("--status") { IsRequired = true };
        var tasksByStatusCmd = new Command("tasks-by-status", "List tasks filtered by status") { statusOpt };
        tasksByStatusCmd.SetHandler(async (string status) =>
        {
            var state = Program.LoadState();
            foreach (var t in state.Tasks.Where(t => string.Equals(t.Status, status, StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"{t.Id}: {t.Description} [{t.Status}]");
            }
            await Task.CompletedTask;
        }, statusOpt);

        var purgeFailedCmd = new Command("purge-failed-tasks", "Remove failed tasks");
        purgeFailedCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            state.Tasks.RemoveAll(t => t.Status == "failed");
            Program.SaveState(state);
            Console.WriteLine("Failed tasks removed");
            await Task.CompletedTask;
        });

        var tasksOverviewCmd = new Command("tasks-overview", "Show count by status");
        tasksOverviewCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var groups = state.Tasks.GroupBy(t => t.Status);
            foreach (var g in groups)
            {
                Console.WriteLine($"{g.Key}: {g.Count()}");
            }
            Console.WriteLine($"Total: {state.Tasks.Count}");
            await Task.CompletedTask;
        });

        var updateDescIdOpt = new Option<string>("--id") { IsRequired = true };
        var updateDescOpt = new Option<string>("--description") { IsRequired = true };
        var updateTaskDescCmd = new Command("update-task-desc", "Update task description")
        {
            updateDescIdOpt, updateDescOpt
        };
        updateTaskDescCmd.SetHandler(async (string id, string description) =>
        {
            var state = Program.LoadState();
            var task = state.Tasks.Find(t => t.Id == id);
            if (task != null)
            {
                task.Description = description;
                task.UpdatedAt = DateTime.UtcNow;
                Program.SaveState(state);
                Console.WriteLine("Description updated");
            }
            else
            {
                Console.WriteLine("Task not found");
            }
            await Task.CompletedTask;
        }, updateDescIdOpt, updateDescOpt);

        var exportTasksPathOpt = new Option<string>("--path") { IsRequired = true };
        var exportTasksCmd = new Command("export-tasks", "Save tasks to JSON file")
        {
            exportTasksPathOpt
        };
        exportTasksCmd.SetHandler(async (string path) =>
        {
            var state = Program.LoadState();
            File.WriteAllText(path, JsonSerializer.Serialize(state.Tasks, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Tasks exported to {path}");
            await Task.CompletedTask;
        }, exportTasksPathOpt);

        var importTasksPathOpt = new Option<string>("--path") { IsRequired = true };
        var importTasksCmd = new Command("import-tasks", "Load tasks from JSON file")
        {
            importTasksPathOpt
        };
        importTasksCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found");
                return;
            }
            var json = File.ReadAllText(path);
            var tasks = JsonSerializer.Deserialize<List<TaskRecord>>(json);
            if (tasks != null)
            {
                var state = Program.LoadState();
                state.Tasks = tasks;
                Program.SaveState(state);
                Console.WriteLine("Tasks imported");
            }
            else
            {
                Console.WriteLine("Invalid tasks file");
            }
            await Task.CompletedTask;
        }, importTasksPathOpt);

        var deleteIdOption = new Option<string>("--id") { IsRequired = true };
        var deleteTaskCmd = new Command("delete-task", "Remove a task")
        {
            deleteIdOption
        };
        deleteTaskCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var idx = state.Tasks.FindIndex(t => t.Id == id);
            if (idx >= 0)
            {
                state.Tasks.RemoveAt(idx);
                Program.SaveState(state);
                Console.WriteLine("Task removed");
            }
            else
            {
                Console.WriteLine("Task not found");
            }
            await Task.CompletedTask;
        }, deleteIdOption);

        var infoIdOption = new Option<string>("--id") { IsRequired = true };
        var taskInfoCmd = new Command("task-info", "Show details for a task")
        {
            infoIdOption
        };
        taskInfoCmd.SetHandler(async (string id) =>
        {
            var state = Program.LoadState();
            var task = state.Tasks.Find(t => t.Id == id);
            if (task != null)
            {
                Console.WriteLine($"Id: {task.Id}\nDescription: {task.Description}\nStatus: {task.Status}");
                if (task.DueDate != null) Console.WriteLine($"Due: {task.DueDate:u}");
                if (task.Tags.Count > 0) Console.WriteLine($"Tags: {string.Join(',', task.Tags)}");
                Console.WriteLine($"Tools: {task.ToolCount}\nInput Tokens: {task.InputTokens}\nOutput Tokens: {task.OutputTokens}");
            }
            else
            {
                Console.WriteLine("Task not found");
            }
            await Task.CompletedTask;
        }, infoIdOption);

        var latestTaskCmd = new Command("latest-task", "Show most recently created task");
        latestTaskCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            var task = state.Tasks.OrderByDescending(t => t.CreatedAt).FirstOrDefault();
            if (task != null)
            {
                Console.WriteLine($"{task.Id}: {task.Description} [{task.Status}]");
            }
            else
            {
                Console.WriteLine("No tasks");
            }
            await Task.CompletedTask;
        });

        var inProgressCmd = new Command("tasks-in-progress", "List IDs of in-progress tasks");
        inProgressCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var t in state.Tasks.Where(t => t.Status == "in-progress"))
                Console.WriteLine(t.Id);
            await Task.CompletedTask;
        });

        var taskDescCmd = new Command("task-descriptions", "List task descriptions");
        taskDescCmd.SetHandler(async () =>
        {
            var state = Program.LoadState();
            foreach (var t in state.Tasks)
                Console.WriteLine(t.Description);
            await Task.CompletedTask;
        });

        root.AddCommand(tasksCmd);
        root.AddCommand(createTaskCmd);
        root.AddCommand(completeTaskCmd);
        root.AddCommand(failTaskCmd);
        root.AddCommand(currentTaskCmd);
        root.AddCommand(setCurrentTaskCmd);
        root.AddCommand(cancelTaskCmd);
        root.AddCommand(taskStatsCmd);
        root.AddCommand(addInputTokensCmd);
        root.AddCommand(addOutputTokensCmd);
        root.AddCommand(taskDurationCmd);
        root.AddCommand(addToolUseCmd);
        root.AddCommand(taskCountCmd);
        root.AddCommand(clearTasksCmd);
        root.AddCommand(clearCompletedCmd);
        root.AddCommand(tasksByStatusCmd);
        root.AddCommand(updateTaskDescCmd);
        root.AddCommand(exportTasksCmd);
        root.AddCommand(importTasksCmd);
        root.AddCommand(purgeFailedCmd);
        root.AddCommand(tasksOverviewCmd);
        root.AddCommand(deleteTaskCmd);
        root.AddCommand(taskInfoCmd);
        root.AddCommand(latestTaskCmd);
        root.AddCommand(inProgressCmd);
        root.AddCommand(taskDescCmd);
    }
}
