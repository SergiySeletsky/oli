public class TaskRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "in-progress";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public List<string> Tags { get; set; } = new();
    public int ToolCount { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int Priority { get; set; } = 0;
    public string Notes { get; set; } = string.Empty;
    public bool Paused { get; set; } = false;
    public bool Archived { get; set; } = false;
}
