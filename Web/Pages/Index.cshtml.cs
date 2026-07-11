using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.BLL.Security;

namespace PRN222_FINAL.Web.Pages;

[Authorize]
public sealed class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        var role = AppRoles.Normalize(User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value);
        return role switch
        {
            AppRoles.Admin => RedirectToPage("/Admin/Statistics", new { tab = "overview", days = 30 }),
            AppRoles.Lecturer => RedirectToPage("/Home/Courses"),
            _ => RedirectToPage("/Home/Chat")
        };
    }
}
