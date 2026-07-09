namespace PRN222_FINAL.Models;

public sealed class CourseSubject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; set; } = true;
    public Guid? OwnerUserId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
    public List<CourseChapter> Chapters { get; set; } = new();
    public int StudentCount { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Code)
        ? Name
        : Code.Equals(Name, StringComparison.OrdinalIgnoreCase)
            ? Code
            : $"{Code} - {Name}";
}
