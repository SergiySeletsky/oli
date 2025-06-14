public class ToolExecution
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TaskId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "running"; // running, success, error
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object>? Metadata { get; set; } = new();
}
