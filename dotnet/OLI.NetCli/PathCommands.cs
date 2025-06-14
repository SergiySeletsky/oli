using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

public static class PathCommands
{
    public static void Register(RootCommand root)
    {
        // open-tasks
        var openTasks = new Command("open-tasks", "Open tasks.json");
        openTasks.SetHandler(() =>
        {
            Process.Start("xdg-open", Program.TasksPath);
            return Task.CompletedTask;
        });

        // open-conversation
        var openConv = new Command("open-conversation", "Open conversation.json");
        openConv.SetHandler(() =>
        {
            Process.Start("xdg-open", Program.ConversationPath);
            return Task.CompletedTask;
        });

        // open-summaries
        var openSums = new Command("open-summaries", "Open summaries.json");
        openSums.SetHandler(() =>
        {
            Process.Start("xdg-open", Program.SummariesPath);
            return Task.CompletedTask;
        });

        // open-lsp
        var openLsp = new Command("open-lsp", "Open lsp.json");
        openLsp.SetHandler(() =>
        {
            Process.Start("xdg-open", Program.LspPath);
            return Task.CompletedTask;
        });

        root.Add(openTasks);
        root.Add(openConv);
        root.Add(openSums);
        root.Add(openLsp);

        // tasks-path
        var tasksPathCmd = new Command("tasks-path", "Show tasks file path");
        tasksPathCmd.SetHandler(() => { Console.WriteLine(Program.TasksPath); return Task.CompletedTask; });

        // conversation-path
        var conversationPathCmd = new Command("conversation-path", "Show conversation file path");
        conversationPathCmd.SetHandler(() => { Console.WriteLine(Program.ConversationPath); return Task.CompletedTask; });

        // summaries-path
        var summariesPathCmd = new Command("summaries-path", "Show summaries file path");
        summariesPathCmd.SetHandler(() => { Console.WriteLine(Program.SummariesPath); return Task.CompletedTask; });

        // tools-path
        var toolsPathCmd = new Command("tools-path", "Show tools file path");
        toolsPathCmd.SetHandler(() => { Console.WriteLine(Program.ToolsPath); return Task.CompletedTask; });

        root.Add(tasksPathCmd);
        root.Add(conversationPathCmd);
        root.Add(summariesPathCmd);
        root.Add(toolsPathCmd);
    }
}
