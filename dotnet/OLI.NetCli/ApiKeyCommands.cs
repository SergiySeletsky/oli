using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;

public static class ApiKeyCommands
{
    const string ApiKeyFile = "apikey.txt";

    public static void Register(RootCommand root)
    {
        // set-api-key
        var setCmd = new Command("set-api-key", "Store API key for provider")
        {
            new Option<string>("--provider", description: "Provider name"),
            new Option<string>("--key", description: "API key")
        };
        setCmd.SetHandler((string provider, string key) =>
        {
            var data = $"{provider}:{key}";
            File.WriteAllText(ApiKeyFile, data);
            Console.WriteLine("API key saved.");
            return Task.CompletedTask;
        }, setCmd.Arguments[0], setCmd.Arguments[1]);

        // get-api-key
        var getCmd = new Command("get-api-key", "Show stored API key");
        getCmd.SetHandler(() =>
        {
            if (File.Exists(ApiKeyFile))
            {
                var data = File.ReadAllText(ApiKeyFile);
                Console.WriteLine(data);
            }
            else Console.WriteLine("No API key stored.");
            return Task.CompletedTask;
        });

        // clear-api-key
        var clearCmd = new Command("clear-api-key", "Remove stored API key");
        clearCmd.SetHandler(() =>
        {
            if (File.Exists(ApiKeyFile)) File.Delete(ApiKeyFile);
            Console.WriteLine("API key cleared.");
            return Task.CompletedTask;
        });

        root.Add(setCmd);
        root.Add(getCmd);
        root.Add(clearCmd);
    }
}
