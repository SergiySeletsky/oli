using System;
using System.IO;
using System.Linq;

public static class MemoryUtils
{
    public static string Head(string path, int lines)
    {
        if (!File.Exists(path)) return string.Empty;
        return string.Join("\n", File.ReadLines(path).Take(lines));
    }

    public static string Tail(string path, int lines)
    {
        if (!File.Exists(path)) return string.Empty;
        var all = File.ReadLines(path).ToList();
        return string.Join("\n", all.Skip(Math.Max(0, all.Count - lines)));
    }
}
