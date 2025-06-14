public class LspServerInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Language { get; set; } = string.Empty; // e.g. python, rust
    public string RootPath { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
}
