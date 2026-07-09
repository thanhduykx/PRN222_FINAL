using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.BLL.Services.Billing;
using PRN222_FINAL.Models;
using PRN222_FINAL.Models.DTOs.Billing;

namespace PRN222_FINAL.Web.Pages.Payments;

[Authorize]
public sealed class ReturnModel : PageModel
{
    private readonly IPaymentService _payments;

    public ReturnModel(IPaymentService payments)
    {
        _payments = payments;
    }

    public PaymentReturnDto? Status { get; private set; }
    public PaymentStatus? ReturnStatusHint { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;

    public async Task OnGetAsync(string? provider, string? orderCode, string? orderId, CancellationToken cancellationToken)
    {
        var rawProvider = string.IsNullOrWhiteSpace(provider) ? QueryValue("provider") : provider;
        var rawOrderCode = !string.IsNullOrWhiteSpace(orderCode) ? orderCode : (!string.IsNullOrWhiteSpace(orderId) ? orderId : QueryValue("orderId"));

        if (!Enum.TryParse<PaymentProvider>(rawProvider, true, out var parsedProvider) || string.IsNullOrWhiteSpace(rawOrderCode))
        {
            ErrorMessage = "Payment return data is missing.";
            return;
        }

        Status = await _payments.GetReturnStatusAsync(parsedProvider, rawOrderCode, cancellationToken);
        ReturnStatusHint = ParseReturnStatusHint(parsedProvider);
        if (Status is null)
        {
            ErrorMessage = "Payment was not found.";
        }
    }

    private string QueryValue(string key) => Request.Query.TryGetValue(key, out var value) ? value.ToString() : string.Empty;

    private PaymentStatus? ParseReturnStatusHint(PaymentProvider provider)
    {
        if (provider == PaymentProvider.PayOS)
        {
            var canceled = QueryValue("cancel");
            var status = QueryValue("status");
            if (canceled.Equals("true", StringComparison.OrdinalIgnoreCase)
                || status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase)
                || status.Equals("CANCELED", StringComparison.OrdinalIgnoreCase))
            {
                return PaymentStatus.Canceled;
            }
        }

        if (provider == PaymentProvider.MoMo)
        {
            var resultCode = QueryValue("resultCode");
            if (!string.IsNullOrWhiteSpace(resultCode)
                && resultCode != "0")
            {
                return PaymentStatus.Failed;
            }
        }

        return null;
    }
}
