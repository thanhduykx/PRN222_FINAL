using PRN222_FINAL.BLL.Services.Accounts;
using PRN222_FINAL.BLL.Security;
using PRN222_FINAL.BLL.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.BLL.Services.Billing;
using PRN222_FINAL.Web.Models;
using PRN222_FINAL.Web.Security;
using PRN222_FINAL.Web.Services;
using PRN222_FINAL.Web.ViewModels.Billing;

namespace PRN222_FINAL.Web.Pages.Subscriptions;

[Authorize]
public sealed class CurrentModel : PageModel
{
    private readonly ISubscriptionService _subscriptions;
    private readonly IPaymentService _payments;
    private readonly IUserAccountService _users;
    private readonly ILogger<CurrentModel> _logger;

    public CurrentModel(ISubscriptionService subscriptions, IPaymentService payments, IUserAccountService users, ILogger<CurrentModel> logger)
    {
        _subscriptions = subscriptions;
        _payments = payments;
        _users = users;
        _logger = logger;
    }

    [BindProperty]
    public ProfileInput Profile { get; set; } = new();

    public SubscriptionViewModel? Subscription { get; private set; }
    public IReadOnlyList<PaymentHistoryItemViewModel> PaymentHistory { get; private set; } = Array.Empty<PaymentHistoryItemViewModel>();
    public string AccountEmail { get; private set; } = string.Empty;
    public string ErrorMessage { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!IsStudent()) return RedirectForRole();
        await LoadPageAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateProfileAsync(CancellationToken cancellationToken)
    {
        if (!IsStudent()) return RedirectForRole();
        if (!ModelState.IsValid)
        {
            await LoadPageAsync(cancellationToken, loadProfile: false);
            return Page();
        }

        try
        {
            var user = await _users.UpdateFullNameAsync(GetUserId(), Profile.FullName, cancellationToken);
            await RefreshAuthenticationAsync(user);
            TempData["ProfileSuccess"] = "Đã cập nhật tên hiển thị.";
            return RedirectToPage();
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError("Profile.FullName", exception.Message);
            await LoadPageAsync(cancellationToken, loadProfile: false);
            return Page();
        }
    }

    private async Task LoadPageAsync(CancellationToken cancellationToken, bool loadProfile = true)
    {
        try
        {
            var userId = GetUserId();
            var user = await _users.FindByIdAsync(userId, cancellationToken);
            AccountEmail = user?.Email ?? User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
            if (loadProfile) Profile.FullName = user?.FullName ?? User.Identity?.Name ?? string.Empty;

            var subscription = await _subscriptions.GetCurrentSubscriptionAsync(userId, cancellationToken);
            Subscription = subscription is null ? null : new SubscriptionViewModel
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
        catch (Exception exception)
        {
            ErrorMessage = "Chưa thể tải thông tin tài khoản và giao dịch. Vui lòng thử lại.";
            _logger.LogWarning(exception, "Could not load account billing page for user {UserId}.", GetUserId());
        }
    }

    private async Task<IReadOnlyList<PaymentHistoryItemViewModel>> LoadPaymentHistoryAsync(Guid userId, CancellationToken cancellationToken)
    {
        var payments = await _payments.GetPaymentsForUserAsync(userId, 20, cancellationToken);
        return payments.Select(payment => new PaymentHistoryItemViewModel
        {
            PaymentId = payment.PaymentId,
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

    private async Task RefreshAuthenticationAsync(UserAccount user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, AppRoles.Normalize(user.Role))
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }

    private Guid GetUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private bool IsStudent() => AppRoles.Normalize(User.FindFirstValue(ClaimTypes.Role)) == AppRoles.Student;
    private IActionResult RedirectForRole() => AppRoles.Normalize(User.FindFirstValue(ClaimTypes.Role)) == AppRoles.Admin
        ? RedirectToPage("/Admin/Statistics") : RedirectToPage("/Home/Courses");

    public sealed class ProfileInput
    {
        [Required(ErrorMessage = "Vui lòng nhập tên hiển thị.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Tên cần có từ 2 đến 120 ký tự.")]
        public string FullName { get; set; } = string.Empty;
    }
}
