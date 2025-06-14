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
        var dlUrlOpt = new Option<string>("--url", "Source URL") { IsRequired = true };
        var dlOutOpt = new Option<string>("--out", "Destination path") { IsRequired = true };
        var dlCmd = new Command("download-file", "Download file from URL")
        {
            dlUrlOpt,
            dlOutOpt
        };
        dlCmd.SetHandler(async (string url, string path) =>
        {
            var data = await Client.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(path, data);
            Console.WriteLine($"Downloaded to {path}");
        }, dlUrlOpt, dlOutOpt);

        // upload-file
        var upUrlOpt = new Option<string>("--url", "Destination URL") { IsRequired = true };
        var upFileOpt = new Option<string>("--file", "File path") { IsRequired = true };
        var upCmd = new Command("upload-file", "POST file to URL")
        {
            upUrlOpt,
            upFileOpt
        };
        upCmd.SetHandler(async (string url, string file) =>
        {
            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(await File.ReadAllBytesAsync(file)), "file", Path.GetFileName(file));
            var res = await Client.PostAsync(url, content);
            Console.WriteLine($"Status: {res.StatusCode}");
        }, upUrlOpt, upFileOpt);

        root.Add(dlCmd);
        root.Add(upCmd);
    }
}
