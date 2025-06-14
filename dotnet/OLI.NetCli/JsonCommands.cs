using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;

public static class JsonCommands
{
    public static void Register(RootCommand root)
    {
        var pathArg = new Argument<string>("path");
        var otherArg = new Argument<string>("other");
        var jsonOpt = new Option<string>("--json") { IsRequired = true };

        var readJsonCmd = new Command("read-json", "Pretty print JSON file") { pathArg };
        readJsonCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }
            Console.WriteLine(JsonUtils.ReadPretty(path));
            await Task.CompletedTask;
        }, pathArg);

        var writeJsonCmd = new Command("write-json", "Write JSON to file") { pathArg, jsonOpt };
        writeJsonCmd.SetHandler(async (string path, string json) =>
        {
            JsonUtils.Write(path, json);
            Console.WriteLine("JSON written");
            await Task.CompletedTask;
        }, pathArg, jsonOpt);

        var formatJsonCmd = new Command("json-format", "Format JSON string") { jsonOpt };
        formatJsonCmd.SetHandler(async (string json) =>
        {
            Console.WriteLine(JsonUtils.Format(json));
            await Task.CompletedTask;
        }, jsonOpt);

        var jsonDiffCmd = new Command("json-diff", "Diff two JSON files") { pathArg, otherArg };
        jsonDiffCmd.SetHandler(async (string path, string other) =>
        {
            if (!File.Exists(path) || !File.Exists(other))
            {
                Console.WriteLine("File not found");
                return;
            }
            Console.WriteLine(JsonUtils.Diff(path, other));
            await Task.CompletedTask;
        }, pathArg, otherArg);

        root.Add(readJsonCmd);
        root.Add(writeJsonCmd);
        root.Add(formatJsonCmd);
        root.Add(jsonDiffCmd);
    }
}
