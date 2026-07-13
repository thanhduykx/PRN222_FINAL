using PRN222_FINAL.BLL.Services.Accounts;
using PRN222_FINAL.BLL.Security;
using PRN222_FINAL.BLL.Models;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.Web.Models;
using PRN222_FINAL.Web.Security;
using PRN222_FINAL.Web.Services;

namespace PRN222_FINAL.Web.Pages.Account;

[AllowAnonymous]
public sealed class LoginModel : PageModel
{
    private const string AccountProvisioningMessage = "Tài khoản được cấp bởi Nhà trường. Vui lòng liên hệ Nhà trường để xin cấp tài khoản.";

    private readonly IUserAccountService _users;
    private readonly IAuthenticationSchemeProvider _schemes;
    private readonly string _googlePublicOrigin;

    public LoginModel(IUserAccountService users, IAuthenticationSchemeProvider schemes, IConfiguration configuration)
    {
        _users = users;
        _schemes = schemes;
        _googlePublicOrigin = (configuration["Authentication:Google:PublicOrigin"] ?? string.Empty).Trim().TrimEnd('/');
    }

    [BindProperty]
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public bool IsGoogleLoginEnabled { get; private set; }
    public string? GoogleLoginUnavailableMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null, string? googleError = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToDefaultDashboard(User.FindFirstValue(ClaimTypes.Role));
        }

        ReturnUrl = returnUrl;
        if (!string.IsNullOrWhiteSpace(googleError))
        {
            TempData["AuthError"] = "Đăng nhập Google không thành công. Vui lòng thử lại hoặc kiểm tra cấu hình callback Google OAuth.";
        }
        await ConfigureGoogleLoginStateAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await ConfigureGoogleLoginStateAsync();
            return Page();
        }

        var user = await _users.FindByEmailAsync(Email, cancellationToken);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, AccountProvisioningMessage);
            await ConfigureGoogleLoginStateAsync();
            return Page();
        }

        if (!_users.VerifyPassword(user, Password))
        {
            ModelState.AddModelError(string.Empty, "The email or password is incorrect.");
            await ConfigureGoogleLoginStateAsync();
            return Page();
        }

        await _users.MarkActiveAsync(user.Id);
        user.LastActiveAt = DateTimeOffset.UtcNow;
        await SignInAsync(user);
        return RedirectAfterSignIn(user, ReturnUrl);
    }

    public async Task<IActionResult> OnPostGoogleLoginAsync(string? returnUrl = null)
    {
        if (await _schemes.GetSchemeAsync(GoogleDefaults.AuthenticationScheme) is null)
        {
            TempData["AuthError"] = "Google sign-in is not configured.";
            return RedirectToPage("/Account/Login", new { returnUrl });
        }

        if (MustRebaseGoogleLoginToPublicOrigin())
        {
            var startUrl = $"{_googlePublicOrigin}/Account/Login?handler=GoogleStart";
            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                startUrl += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
            }
            return Redirect(startUrl);
        }

        return CreateGoogleChallenge(returnUrl);
    }

    public async Task<IActionResult> OnGetGoogleStartAsync(string? returnUrl = null)
    {
        if (await _schemes.GetSchemeAsync(GoogleDefaults.AuthenticationScheme) is null)
        {
            TempData["AuthError"] = "Google sign-in is not configured.";
            return RedirectToPage("/Account/Login", new { returnUrl });
        }

        return CreateGoogleChallenge(returnUrl);
    }

    private ChallengeResult CreateGoogleChallenge(string? returnUrl)
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Page("/Account/GoogleCallback", pageHandler: null, values: new { returnUrl }, protocol: Request.Scheme)
        };

        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    private bool MustRebaseGoogleLoginToPublicOrigin()
    {
        if (string.IsNullOrWhiteSpace(_googlePublicOrigin)
            || !Uri.TryCreate(_googlePublicOrigin, UriKind.Absolute, out var publicOrigin))
        {
            return false;
        }

        return !Request.Scheme.Equals(publicOrigin.Scheme, StringComparison.OrdinalIgnoreCase)
               || !Request.Host.Host.Equals(publicOrigin.Host, StringComparison.OrdinalIgnoreCase)
               || Request.Host.Port != (publicOrigin.IsDefaultPort ? null : publicOrigin.Port);
    }

    private async Task SignInAsync(UserAccount user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, AppRoles.Normalize(user.Role))
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            });
    }

    private IActionResult RedirectAfterSignIn(UserAccount user, string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl)
            && Url.IsLocalUrl(returnUrl)
            && CanAccessReturnUrl(AppRoles.Normalize(user.Role), returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToDefaultDashboard(user.Role);
    }

    private IActionResult RedirectToDefaultDashboard(string? role)
    {
        return AppRoles.Normalize(role) switch
        {
            AppRoles.Admin => RedirectToPage("/Admin/Statistics", new { tab = "overview", days = 30 }),
            AppRoles.Lecturer => RedirectToPage("/Home/Courses"),
            _ => RedirectToPage("/Home/Chat")
        };
    }

    private static bool CanAccessReturnUrl(string role, string returnUrl)
    {
        if (role == AppRoles.Admin)
        {
            return true;
        }

        var path = returnUrl.Split('?', '#')[0].TrimEnd('/');
        if (string.IsNullOrWhiteSpace(path))
        {
            path = "/";
        }

        if (role == AppRoles.Lecturer)
        {
            return !path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase);
        }

        return path.Equals("/Home/Chat", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/Home/Chat/", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ConfigureGoogleLoginStateAsync()
    {
        GoogleLoginUnavailableMessage = null;
        IsGoogleLoginEnabled = await _schemes.GetSchemeAsync(GoogleDefaults.AuthenticationScheme) is not null;

    }
}

