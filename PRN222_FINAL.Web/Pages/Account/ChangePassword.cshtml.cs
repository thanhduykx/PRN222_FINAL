using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.Web.Models;
using PRN222_FINAL.Web.Services;

namespace PRN222_FINAL.Web.Pages.Account;

[Authorize]
public sealed class ChangePasswordModel : PageModel
{
    private readonly IUserAccountStore _users;

    public ChangePasswordModel(IUserAccountStore users)
    {
        _users = users;
    }

    [BindProperty]
    public ChangePasswordViewModel Input { get; set; } = new();

    public string? StatusMessage { get; private set; }

    public void OnGet()
    {
        StatusMessage = TempData["AccountSuccess"] as string;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Challenge();
        }

        try
        {
            await _users.ChangePasswordAsync(userId, Input.CurrentPassword, Input.NewPassword, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ToVietnamesePasswordError(ex.Message));
            return Page();
        }

        TempData["AccountSuccess"] = "Äá»•i máº­t kháº©u thÃ nh cÃ´ng.";
        return RedirectToPage();
    }

    private static string ToVietnamesePasswordError(string message)
    {
        if (message.Contains("does not have a local password", StringComparison.OrdinalIgnoreCase))
        {
            return "TÃ i khoáº£n nÃ y chÆ°a cÃ³ máº­t kháº©u ná»™i bá»™. DÃ¹ng chá»©c nÄƒng quÃªn máº­t kháº©u Ä‘á»ƒ táº¡o máº­t kháº©u.";
        }

        if (message.Contains("Current password is incorrect", StringComparison.OrdinalIgnoreCase))
        {
            return "Máº­t kháº©u hiá»‡n táº¡i khÃ´ng Ä‘Ãºng.";
        }

        if (message.Contains("at least 8 characters", StringComparison.OrdinalIgnoreCase))
        {
            return "Máº­t kháº©u má»›i pháº£i cÃ³ Ã­t nháº¥t 8 kÃ½ tá»±.";
        }

        return string.IsNullOrWhiteSpace(message) ? "KhÃ´ng thá»ƒ Ä‘á»•i máº­t kháº©u." : message;
    }
}

