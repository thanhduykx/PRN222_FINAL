using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.BLL.Services.Billing;
using PRN222_FINAL.BLL.Models;
using PRN222_FINAL.BLL.Contracts.Billing;

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
            ErrorMessage = "Thiếu thông tin giao dịch từ cổng thanh toán. Vui lòng quay lại trang chọn gói.";
            return;
        }

        if (parsedProvider == PaymentProvider.MoMo && !string.IsNullOrWhiteSpace(QueryValue("signature")))
        {
            try
            {
                await _payments.HandleSignedReturnAsync(new PaymentWebhookDto
                {
                    Provider = PaymentProvider.MoMo,
                    RawBody = Request.QueryString.Value ?? string.Empty,
                    Values = Request.Query.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value.ToString(),
                        StringComparer.Ordinal)
                }, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                ErrorMessage = "Không thể xác minh kết quả trả về từ MoMo. Vui lòng kiểm tra lịch sử giao dịch hoặc liên hệ hỗ trợ.";
            }
        }

        Status = await _payments.GetReturnStatusAsync(parsedProvider, rawOrderCode, cancellationToken);
        ReturnStatusHint = ParseReturnStatusHint(parsedProvider);
        if (Status is null)
        {
            ErrorMessage = "Không tìm thấy giao dịch tương ứng. Nếu bạn đã thanh toán, vui lòng kiểm tra lại sau ít phút.";
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
