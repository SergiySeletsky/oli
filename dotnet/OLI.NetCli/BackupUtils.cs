using System;
using System.IO;
using System.Linq;

public static class BackupUtils
{
    public static readonly string BackupDir = Path.Combine(AppContext.BaseDirectory, "backups");

    public static string BackupFile(string sourcePath, string prefix)
    {
        Directory.CreateDirectory(BackupDir);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var dest = Path.Combine(BackupDir, $"{prefix}_{timestamp}");
        File.Copy(sourcePath, dest, true);
        return dest;
    }

    public static void RestoreFile(string backupPath, string targetPath)
    {
        File.Copy(backupPath, targetPath, true);
    }

    public static string? LatestBackup()
    {
        if (!Directory.Exists(BackupDir)) return null;
        return Directory.GetFiles(BackupDir)
            .OrderByDescending(f => f)
            .FirstOrDefault();
    }
}
