using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.Web.Models;
using PRN222_FINAL.Web.Services;

namespace PRN222_FINAL.Web.Pages.Account;

[AllowAnonymous]
public sealed class ResetPasswordModel : PageModel
{
    private readonly IUserAccountStore _users;

    public ResetPasswordModel(IUserAccountStore users)
    {
        _users = users;
    }

    [BindProperty]
    public ResetPasswordViewModel Input { get; set; } = new();

    public IActionResult OnGet(string? email, string? token)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            TempData["AuthError"] = "LiÃªn káº¿t Ä‘áº·t láº¡i máº­t kháº©u khÃ´ng há»£p lá»‡.";
            return RedirectToPage("/Account/ForgotPassword");
        }

        Input.Email = email;
        Input.Token = token;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await _users.ResetPasswordAsync(Input.Email, Input.Token, Input.Password, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ToVietnamesePasswordError(ex.Message));
            return Page();
        }

        TempData["AuthSuccess"] = "Äáº·t láº¡i máº­t kháº©u thÃ nh cÃ´ng. Báº¡n cÃ³ thá»ƒ Ä‘Äƒng nháº­p báº±ng máº­t kháº©u má»›i.";
        return RedirectToPage("/Account/Login");
    }

    private static string ToVietnamesePasswordError(string message)
    {
        if (message.Contains("invalid or expired", StringComparison.OrdinalIgnoreCase))
        {
            return "LiÃªn káº¿t Ä‘áº·t láº¡i máº­t kháº©u khÃ´ng há»£p lá»‡ hoáº·c Ä‘Ã£ háº¿t háº¡n.";
        }

        if (message.Contains("at least 8 characters", StringComparison.OrdinalIgnoreCase))
        {
            return "Máº­t kháº©u pháº£i cÃ³ Ã­t nháº¥t 8 kÃ½ tá»±.";
        }

        return string.IsNullOrWhiteSpace(message) ? "KhÃ´ng thá»ƒ Ä‘áº·t láº¡i máº­t kháº©u." : message;
    }
}

