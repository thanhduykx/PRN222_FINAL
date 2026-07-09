using PRN222_FINAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.Web.Security;
using PRN222_FINAL.Web.Services;
using PRN222_FINAL.BLL;

namespace PRN222_FINAL.Web.Pages.Home;

[IgnoreAntiforgeryToken]
[Authorize(Policy = AuthorizationPolicies.ChatAccess)]
public sealed class CreateChatSessionModel : HomePageModelBase
{
    public CreateChatSessionModel(
        ILogger<HomePageModelBase> logger,
        IKnowledgeService knowledge,
        IDocumentIndexingService indexingService,
        IWebPageTextExtractor webPageTextExtractor,
        IRagChatService chatService,
        IUserAccountStore users,
        IWebHostEnvironment environment,
        IDocumentIndexJobQueue indexJobQueue)
        : base(logger, knowledge, indexingService, webPageTextExtractor, chatService, users, environment, indexJobQueue)
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var session = await _knowledge.GetOrCreateSessionAsync(Guid.NewGuid(), cancellationToken, BuildChatSessionOwnerInfo());
        return new JsonResult(ToSessionSummary(session));
    }
}

