using PRN222_FINAL.BLL.Services.Accounts;
using PRN222_FINAL.BLL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_FINAL.Web.Security;
using PRN222_FINAL.Web.Services;
using PRN222_FINAL.BLL;

namespace PRN222_FINAL.Web.Pages.Home;

[Authorize(Policy = AuthorizationPolicies.ChatAccess)]
public sealed class ChatSessionsModel : HomePageModelBase
{
    public ChatSessionsModel(
        ILogger<ChatSessionsModel> logger,
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

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAccountAsync(cancellationToken);
        var sessions = currentUser is null
            ? Array.Empty<ChatSessionSummary>()
            : await _knowledge.GetSessionSummariesForOwnerAsync(currentUser.Id, cancellationToken);
        return new JsonResult(sessions.Select(ToSessionSummary));
    }
}

