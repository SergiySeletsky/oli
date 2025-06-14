using System;
using System.CommandLine;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

public static class NetworkCommands
{
    static readonly HttpClient Client = new();

    public static void Register(RootCommand root)
    {
        // download-file
        var dlCmd = new Command("download-file", "Download file from URL")
        {
            new Option<string>("--url", description: "Source URL"),
            new Option<string>("--out", description: "Destination path")
        };
        dlCmd.SetHandler(async (string url, string path) =>
        {
            var data = await Client.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(path, data);
            Console.WriteLine($"Downloaded to {path}");
        }, dlCmd.Arguments[0], dlCmd.Arguments[1]);

        // upload-file
        var upCmd = new Command("upload-file", "POST file to URL")
        {
            new Option<string>("--url", description: "Destination URL"),
            new Option<string>("--file", description: "File path")
        };
        upCmd.SetHandler(async (string url, string file) =>
        {
            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(await File.ReadAllBytesAsync(file)), "file", Path.GetFileName(file));
            var res = await Client.PostAsync(url, content);
            Console.WriteLine($"Status: {res.StatusCode}");
        }, upCmd.Arguments[0], upCmd.Arguments[1]);

        root.Add(dlCmd);
        root.Add(upCmd);
    }
}
