using System.Collections.Generic;
using System.IO;

public class AppState
{
    public bool AgentMode { get; set; }
    public int SelectedModel { get; set; }
    public List<string> Conversation { get; set; } = new();
    public List<ConversationSummary> ConversationSummaries { get; set; } = new();
    public List<TaskRecord> Tasks { get; set; } = new();
    public string? CurrentTaskId { get; set; }
    public List<ToolExecution> ToolExecutions { get; set; } = new();
    public HashSet<string> Subscriptions { get; set; } = new();
    public bool AutoCompress { get; set; } = false;
    public int CompressCharThreshold { get; set; } = 4000;
    public int CompressMessageThreshold { get; set; } = 50;
    public string WorkingDirectory { get; set; } = Directory.GetCurrentDirectory();
    public List<LspServerInfo> LspServers { get; set; } = new();
}
