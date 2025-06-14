using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

public static class LogCommands
{
    public static string LogPath => Path.Combine(AppContext.BaseDirectory, "app.log");

    public static void Register(RootCommand root)
    {
        // open-log
        var openCmd = new Command("open-log", "Open log file in default viewer");
        openCmd.SetHandler(() => { Process.Start("xdg-open", LogPath); return Task.CompletedTask; });

        // rotate-log
        var rotateCmd = new Command("rotate-log", "Rotate log file");
        rotateCmd.SetHandler(() =>
        {
            if (File.Exists(LogPath))
            {
                var ts = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                var dest = Path.Combine(Path.GetDirectoryName(LogPath)!, $"app-{ts}.log");
                File.Move(LogPath, dest);
                Console.WriteLine($"Rotated to {dest}");
            }
            return Task.CompletedTask;
        });

        // log-size
        var sizeCmd = new Command("log-size", "Display log file size");
        sizeCmd.SetHandler(() =>
        {
            if (File.Exists(LogPath))
            {
                var len = new FileInfo(LogPath).Length;
                Console.WriteLine(len);
            }
            else Console.WriteLine("0");
            return Task.CompletedTask;
        });

        root.Add(openCmd);
        root.Add(rotateCmd);
        root.Add(sizeCmd);
    }
}
