using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.Web.Security;

namespace PRN222_FINAL.Web.Pages.Home;

[Authorize(Policy = AuthorizationPolicies.ChatAccess)]
public sealed class OnlineUsersModel : PageModel
{
}

