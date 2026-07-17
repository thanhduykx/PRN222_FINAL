using System.Globalization;
using PRN222_FINAL.DAL.Entities.Billing;

namespace PRN222_FINAL.DAL.Repositories.Billing;

internal static class PackagePriceChangeNotificationFormatter
{
    internal const string ReasonLabel = "Lý do thay đổi:";
    private static readonly CultureInfo PriceCulture = CultureInfo.GetCultureInfo("vi-VN");

    public static string Create(KnowledgeSqlPackagePriceChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        var message = $"Giá gói {change.PackageName} đã thay đổi từ {change.OldPriceVnd.ToString("N0", PriceCulture)}đ thành {change.NewPriceVnd.ToString("N0", PriceCulture)}đ. Giá mới áp dụng cho các giao dịch tiếp theo.";
        return AppendReason(message, change.Reason);
    }

    public static string AppendReason(string message, string? reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason)
            || message.Contains(ReasonLabel, StringComparison.OrdinalIgnoreCase))
        {
            return message;
        }

        return $"{message.Trim()} {ReasonLabel} {normalizedReason}";
    }
}
