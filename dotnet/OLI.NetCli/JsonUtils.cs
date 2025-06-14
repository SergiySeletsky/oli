using System;
using System.IO;
using System.Text.Json;

public static class JsonUtils
{
    public static string ReadPretty(string path)
    {
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
    }

    public static void Write(string path, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var pretty = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, pretty);
    }

    public static string Format(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string Diff(string pathA, string pathB)
    {
        var prettyA = ReadPretty(pathA);
        var prettyB = ReadPretty(pathB);
        var linesA = prettyA.Split('\n');
        var linesB = prettyB.Split('\n');
        return Program.GenerateDiff(linesA, linesB);
    }
}
