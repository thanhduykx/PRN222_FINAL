namespace PRN222_FINAL.BLL.Contracts.Billing;

public sealed class PackageDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PriceVnd { get; set; }
    public int DurationDays { get; set; }
    public int MonthlyChatLimit { get; set; }
    public int MonthlyDocumentUploadLimit { get; set; }
    public int StorageLimitMb { get; set; }
    public bool IsLifetime { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}
