namespace PRN222_FINAL.Web.ViewModels.Billing;

public sealed class PackageViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PriceVnd { get; set; }
    public int DurationDays { get; set; }
    public int MonthlyChatLimit { get; set; }
    public bool IsLifetime { get; set; }
    public bool IsCurrentPackage { get; set; }
    public bool IsFree => PriceVnd <= 0;
    public string DurationLabel => IsLifetime ? "Vĩnh viễn" : $"{DurationDays} ngày";
}
