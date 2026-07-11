namespace PRN222_FINAL.Web.ViewModels.Billing;

public sealed class SubscriptionViewModel
{
    public Guid PackageId { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public int MonthlyChatLimit { get; set; }
    public bool IsLifetime { get; set; }
    public string ValidUntilLabel => IsLifetime ? "Vĩnh viễn" : EndsAt.LocalDateTime.ToString("dd/MM/yyyy HH:mm");
}

public sealed class PaymentHistoryItemViewModel
{
    public Guid PaymentId { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public string PackageCode { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal AmountVnd { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public string FailureReason { get; set; } = string.Empty;
}
