public class TaskRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "in-progress";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int ToolCount { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}
