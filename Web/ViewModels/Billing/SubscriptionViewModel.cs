namespace PRN222_FINAL.Web.ViewModels.Billing;

public sealed class SubscriptionViewModel
{
    public string PackageName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public int MonthlyChatLimit { get; set; }
    public int MonthlyDocumentUploadLimit { get; set; }
    public int StorageLimitMb { get; set; }
}
