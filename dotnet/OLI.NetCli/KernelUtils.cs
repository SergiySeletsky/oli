using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

public static class KernelUtils
{
    static Kernel? _kernel;

    static Kernel GetKernel()
    {
        if (_kernel != null) return _kernel;
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("OPENAI_API_KEY not set");
        _kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion("gpt-3.5-turbo", apiKey)
            .Build();
        return _kernel;
    }

    public static async Task<string> CompleteAsync(string prompt)
    {
        var kernel = GetKernel();
        var result = await kernel.InvokePromptAsync(prompt);
        return result.GetValue<string>() ?? string.Empty;
    }

    public static async Task<string> SummarizeAsync(string text)
    {
        var prompt = $"Summarize the following text in a concise paragraph:\n{text}";
        return await CompleteAsync(prompt);
    }
}
