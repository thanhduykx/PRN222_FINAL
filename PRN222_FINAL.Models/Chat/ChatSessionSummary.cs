namespace PRN222_FINAL.Models;

public sealed class ChatSessionSummary
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public bool IsStarred { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public int MessageCount { get; set; }
    public string FirstUserMessagePreview { get; set; } = string.Empty;
}
