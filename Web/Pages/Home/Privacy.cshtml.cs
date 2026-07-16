using Microsoft.AspNetCore.Authorization;
using PRN222_FINAL.Web.Security;

namespace PRN222_FINAL.Web.Pages.Home;

[Authorize(Policy = AuthorizationPolicies.ChatAccess)]
public sealed class PrivacyModel : Microsoft.AspNetCore.Mvc.RazorPages.PageModel
{
}

