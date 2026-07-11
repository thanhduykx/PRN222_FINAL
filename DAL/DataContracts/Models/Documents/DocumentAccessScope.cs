namespace PRN222_FINAL.DAL.Models;

public sealed record DocumentAccessScope(
    string Role,
    Guid? UserId,
    string? Email,
    DocumentAccessMode Mode)
{
    public bool IsAdmin => Role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
    public bool IsLecturer => Role.Equals("Lecturer", StringComparison.OrdinalIgnoreCase);
    public bool IsStudent => Role.Equals("Student", StringComparison.OrdinalIgnoreCase);
    public string NormalizedEmail => (Email ?? string.Empty).Trim();
}
