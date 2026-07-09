using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PRN222_FINAL.DAL.Entities.Billing;

[Table("packages")]
public sealed class KnowledgeSqlPackage
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "numeric(18,2)")]
    public decimal PriceVnd { get; set; }

    public int DurationDays { get; set; }
    public int MonthlyChatLimit { get; set; }
    public int MonthlyDocumentUploadLimit { get; set; }
    public int StorageLimitMb { get; set; }
    public bool IsLifetime { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
