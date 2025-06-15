using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

public static class BackupCommands
{
    public static void Register(RootCommand root)
    {
        var backupPathCmd = new Command("backup-path", "Show backups directory path");
        backupPathCmd.SetHandler(() =>
        {
            Console.WriteLine(BackupUtils.BackupDir);
            return Task.CompletedTask;
        });

        var openBackupsCmd = new Command("open-backups", "Open backups directory");
        openBackupsCmd.SetHandler(() =>
        {
            Directory.CreateDirectory(BackupUtils.BackupDir);
            Process.Start("xdg-open", BackupUtils.BackupDir);
            return Task.CompletedTask;
        });

        root.AddCommand(backupPathCmd);
        root.AddCommand(openBackupsCmd);
    }
}
