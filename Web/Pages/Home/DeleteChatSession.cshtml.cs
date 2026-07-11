using PRN222_FINAL.BLL.Services.Accounts;
using PRN222_FINAL.BLL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.Web.Models;
using PRN222_FINAL.Web.Security;
using PRN222_FINAL.Web.Services;
using PRN222_FINAL.BLL;

namespace PRN222_FINAL.Web.Pages.Home;

[IgnoreAntiforgeryToken]
[Authorize(Policy = AuthorizationPolicies.ChatAccess)]
public sealed class DeleteChatSessionModel : HomePageModelBase
{
    public DeleteChatSessionModel(
        ILogger<HomePageModelBase> logger,
        IKnowledgeService knowledge,
        IDocumentIndexingService indexingService,
        IWebPageTextExtractor webPageTextExtractor,
        IRagChatService chatService,
        IUserAccountService users,
        IWebHostEnvironment environment,
        IDocumentIndexJobQueue indexJobQueue)
        : base(logger, knowledge, indexingService, webPageTextExtractor, chatService, users, environment, indexJobQueue)
    {
    }

    public async Task<IActionResult> OnPostAsync([FromBody] ChatSessionDeleteRequest? request, CancellationToken cancellationToken)
    {
        if (request is null || !Guid.TryParse(request.SessionId, out var sessionId))
        {
            return BadRequest(new { error = "Invalid chat session." });
        }

        try
        {
            var deleted = await _knowledge.DeleteSessionAsync(sessionId, cancellationToken, BuildChatSessionOwnerInfo());
            return deleted
                ? new JsonResult(new { deleted = true, sessionId })
                : NotFound(new { error = "Chat session not found." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

