using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.BLL.Services.Billing;
using PRN222_FINAL.Web.Security;
using PRN222_FINAL.Web.ViewModels.Billing;

namespace PRN222_FINAL.Web.Pages.Subscriptions;

[Authorize]
public sealed class CurrentModel : PageModel
{
    private readonly ISubscriptionService _subscriptions;
    private readonly IPaymentService _payments;

    public CurrentModel(ISubscriptionService subscriptions, IPaymentService payments)
    {
        _subscriptions = subscriptions;
        _payments = payments;
    }

    public SubscriptionViewModel? Subscription { get; private set; }
    public IReadOnlyList<PaymentHistoryItemViewModel> PaymentHistory { get; private set; } = Array.Empty<PaymentHistoryItemViewModel>();
    public string ErrorMessage { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!IsStudent())
        {
            return RedirectForRole();
        }

        try
        {
            var userId = GetUserId();
            var subscription = await _subscriptions.GetCurrentSubscriptionAsync(userId, cancellationToken);
            if (subscription is null)
            {
                PaymentHistory = await LoadPaymentHistoryAsync(userId, cancellationToken);
                return Page();
            }

            Subscription = new SubscriptionViewModel
            {
                PackageId = subscription.PackageId,
                PackageName = subscription.PackageName,
                Status = subscription.Status.ToString(),
                StartsAt = subscription.StartsAt,
                EndsAt = subscription.EndsAt,
                MonthlyChatLimit = subscription.MonthlyChatLimit,
                IsLifetime = subscription.IsLifetime
            };
            PaymentHistory = await LoadPaymentHistoryAsync(userId, cancellationToken);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        return Page();
    }

    private async Task<IReadOnlyList<PaymentHistoryItemViewModel>> LoadPaymentHistoryAsync(Guid userId, CancellationToken cancellationToken)
    {
        var payments = await _payments.GetPaymentsForUserAsync(userId, 20, cancellationToken);
        return payments.Select(payment => new PaymentHistoryItemViewModel
        {
            PackageName = string.IsNullOrWhiteSpace(payment.PackageName) ? payment.PackageCode : payment.PackageName,
            PackageCode = payment.PackageCode,
            Provider = payment.Provider.ToString(),
            Status = payment.Status.ToString(),
            AmountVnd = payment.AmountVnd,
            OrderCode = payment.OrderCode,
            CreatedAt = payment.CreatedAt,
            PaidAt = payment.PaidAt,
            FailureReason = payment.FailureReason
        }).ToList();
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : Guid.Empty;
    }

    private bool IsStudent()
    {
        return AppRoles.Normalize(User.FindFirstValue(ClaimTypes.Role)) == AppRoles.Student;
    }

    private IActionResult RedirectForRole()
    {
        return AppRoles.Normalize(User.FindFirstValue(ClaimTypes.Role)) == AppRoles.Admin
            ? RedirectToPage("/Admin/Statistics")
            : RedirectToPage("/Home/Courses");
    }
}
