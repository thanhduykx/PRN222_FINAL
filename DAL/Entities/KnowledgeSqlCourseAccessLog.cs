using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PRN222_FINAL.DAL.Entities;

[Table("course_access_logs")]
public sealed class KnowledgeSqlCourseAccessLog
{
    [Key]
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    [MaxLength(255)]
    public string UserName { get; set; } = string.Empty;

    [MaxLength(255)]
    public string UserEmail { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Role { get; set; } = string.Empty;

    public Guid SubjectId { get; set; }

    [MaxLength(64)]
    public string SubjectCode { get; set; } = string.Empty;

    [MaxLength(255)]
    public string SubjectName { get; set; } = string.Empty;

    [MaxLength(64)]
    public string AccessArea { get; set; } = string.Empty;

    public DateTimeOffset AccessedAt { get; set; } = DateTimeOffset.UtcNow;
}
