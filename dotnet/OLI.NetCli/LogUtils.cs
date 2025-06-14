using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class LogUtils
{
    public static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "oli.log");

    public static void Log(string message)
    {
        File.AppendAllText(LogPath, $"{DateTime.UtcNow:o} {message}\n");
    }

    public static string ReadLog(int lines = 20)
    {
        if (!File.Exists(LogPath)) return string.Empty;
        var all = File.ReadAllLines(LogPath);
        return string.Join('\n', all.TakeLast(lines));
    }

    public static void ClearLog()
    {
        if (File.Exists(LogPath)) File.WriteAllText(LogPath, string.Empty);
    }

    public static IEnumerable<string> SearchLog(string query)
    {
        return File.Exists(LogPath)
            ? File.ReadLines(LogPath).Where(l => l.Contains(query, StringComparison.OrdinalIgnoreCase))
            : Array.Empty<string>();
    }

    public static void ExportLog(string path)
    {
        if (File.Exists(LogPath))
        {
            File.Copy(LogPath, path, true);
        }
    }
}
