using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PRN222_FINAL.Web.Pages.Account;

[AllowAnonymous]
public sealed class RegisterModel : PageModel
{
    private const string AccountProvisioningMessage = "TÃ i khoáº£n Ä‘Æ°á»£c cáº¥p bá»Ÿi NhÃ  trÆ°á»ng. Vui lÃ²ng liÃªn há»‡ NhÃ  trÆ°á»ng Ä‘á»ƒ xin cáº¥p tÃ i khoáº£n.";

    [BindProperty]
    public string FullName { get; set; } = string.Empty;

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public string ConfirmPassword { get; set; } = string.Empty;

    public IActionResult OnGet()
    {
        TempData["AuthError"] = AccountProvisioningMessage;
        return RedirectToPage("/Account/Login");
    }

    public IActionResult OnPost()
    {
        TempData["AuthError"] = AccountProvisioningMessage;
        return RedirectToPage("/Account/Login");
    }
}

