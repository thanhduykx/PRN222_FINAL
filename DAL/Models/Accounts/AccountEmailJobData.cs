namespace PRN222_FINAL.DAL.Models.Accounts;

public sealed class AccountEmailJobData
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string ApplicationBaseUrl { get; set; } = string.Empty;
    public string SubjectLabelsJson { get; set; } = "[]";
    public int Attempts { get; set; }
    public DateTimeOffset AvailableAt { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string LastError { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
