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

        root.Add(openTasks);
        root.Add(openConv);
    }
}
