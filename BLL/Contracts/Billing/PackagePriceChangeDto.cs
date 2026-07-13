namespace PRN222_FINAL.BLL.Contracts.Billing;

public sealed class PackagePriceChangeDto
{
    public Guid Id { get; set; }
    public Guid PackageId { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public decimal OldPriceVnd { get; set; }
    public decimal NewPriceVnd { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset ChangedAt { get; set; }
}
