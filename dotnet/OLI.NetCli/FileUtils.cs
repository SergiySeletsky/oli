using System;
using System.IO;

public static class FileUtils
{
    public static void Touch(string path)
    {
        if (!File.Exists(path))
        {
            using File.Create(path) { }
        }
        else
        {
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        }
    }

    public static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var target = Path.Combine(destDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    public static void MoveDirectory(string sourceDir, string destDir)
    {
        CopyDirectory(sourceDir, destDir);
        Directory.Delete(sourceDir, true);
    }

    public static void RenameDirectory(string sourceDir, string newName)
    {
        var parent = Directory.GetParent(sourceDir)?.FullName ?? ".";
        var dest = Path.Combine(parent, newName);
        Directory.Move(sourceDir, dest);
    }
}
