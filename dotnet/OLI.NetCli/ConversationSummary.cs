public class ConversationSummary
{
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int MessagesCount { get; set; }
    public int OriginalChars { get; set; }
}
