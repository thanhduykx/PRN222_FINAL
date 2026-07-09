using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.BLL.Services.Billing;
using PRN222_FINAL.Web.ViewModels.Billing;

namespace PRN222_FINAL.Web.Pages.Subscriptions;

[Authorize]
public sealed class CurrentModel : PageModel
{
    private readonly ISubscriptionService _subscriptions;

    public CurrentModel(ISubscriptionService subscriptions)
    {
        _subscriptions = subscriptions;
    }

    public SubscriptionViewModel? Subscription { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var subscription = await _subscriptions.GetCurrentSubscriptionAsync(GetUserId(), cancellationToken);
            if (subscription is null)
            {
                return;
            }

            Subscription = new SubscriptionViewModel
            {
                PackageName = subscription.PackageName,
                Status = subscription.Status.ToString(),
                StartsAt = subscription.StartsAt,
                EndsAt = subscription.EndsAt,
                MonthlyChatLimit = subscription.MonthlyChatLimit,
                MonthlyDocumentUploadLimit = subscription.MonthlyDocumentUploadLimit,
                StorageLimitMb = subscription.StorageLimitMb
            };
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : Guid.Empty;
    }
}
