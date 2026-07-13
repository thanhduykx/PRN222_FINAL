using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PRN222_FINAL.DAL.Entities;

[Table("rag_test_questions")]
public sealed class KnowledgeSqlTestQuestion
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(255)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Question { get; set; } = string.Empty;

    [Required]
    public string GroundTruth { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Difficulty { get; set; } = "Medium";

    [MaxLength(100)]
    public string? Category { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
