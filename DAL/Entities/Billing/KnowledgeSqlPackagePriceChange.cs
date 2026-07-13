using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PRN222_FINAL.DAL.Entities.Billing;

[Table("package_price_changes")]
public sealed class KnowledgeSqlPackagePriceChange
{
    [Key]
    public Guid Id { get; set; }

    public Guid PackageId { get; set; }

    [Required, MaxLength(120)]
    public string PackageName { get; set; } = string.Empty;

    [Column(TypeName = "numeric(18,2)")]
    public decimal OldPriceVnd { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal NewPriceVnd { get; set; }

    [Required, MaxLength(255)]
    public string ChangedBy { get; set; } = string.Empty;

    public DateTimeOffset ChangedAt { get; set; }
}
