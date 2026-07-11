using PRN222_FINAL.BLL.Services.Chat;
using PRN222_FINAL.BLL.Services.Accounts;
using PRN222_FINAL.BLL.Security;
using PRN222_FINAL.BLL.Models;
using Microsoft.AspNetCore.Authorization;
using PRN222_FINAL.Web.Models;
using PRN222_FINAL.Web.Security;
using PRN222_FINAL.Web.Services;
using PRN222_FINAL.BLL;

namespace PRN222_FINAL.Web.Pages.Home;

[Authorize(Policy = AuthorizationPolicies.ChatAccess)]
public sealed class ChatModel : HomePageModelBase
{
    public ChatModel(
        ILogger<HomePageModelBase> logger,
        IKnowledgeService knowledge,
        IDocumentIndexingService indexingService,
        IWebPageTextExtractor webPageTextExtractor,
        IRagChatService chatService,
        IUserAccountService users,
        IWebHostEnvironment environment,
        IDocumentIndexJobQueue indexJobQueue,
        IChatUsageService chatUsage)
        : base(logger, knowledge, indexingService, webPageTextExtractor, chatService, users, environment, indexJobQueue)
    {
        _chatUsage = chatUsage;
    }

    private readonly IChatUsageService _chatUsage;

    public IReadOnlyList<ChatSessionSummary> ChatSessions { get; private set; } = Array.Empty<ChatSessionSummary>();
    public IReadOnlyList<IndexedDocument> Documents { get; private set; } = Array.Empty<IndexedDocument>();
    public IReadOnlyList<string> SubjectOptions { get; private set; } = Array.Empty<string>();
    public string? LoadErrorMessage { get; private set; }
    public ChatUsage Usage { get; private set; } = new(null, 0, null, "");

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAccountAsync(cancellationToken);
        if (currentUser is not null && CurrentRole() == AppRoles.Student)
        {
            Usage = await _chatUsage.GetAsync(currentUser.Id, cancellationToken);
        }
        var chatScope = BuildDocumentAccessScope(DocumentAccessMode.Chat);
        IReadOnlyList<string> subjectOptions;
        IReadOnlyList<IndexedDocument> indexedDocuments;
        try
        {
            subjectOptions = await _knowledge.GetIndexedSubjectsAsync(chatScope, cancellationToken);
            indexedDocuments = CurrentRole() == AppRoles.Student
                ? Array.Empty<IndexedDocument>()
                : await _knowledge.GetDocumentsAsync(
                    chatScope,
                    new DocumentListQuery(StatusFilter: DocumentIndexStatus.Indexed),
                    cancellationToken);
        }
        catch (Exception ex) when (IsDataAccessTimeout(ex))
        {
            _logger.LogWarning(ex, "Chat page could not load indexed subject metadata because the database was unavailable.");
            subjectOptions = Array.Empty<string>();
            indexedDocuments = Array.Empty<IndexedDocument>();
            LoadErrorMessage = "Database unavailable/timeout. Chat metadata could not be loaded.";
        }

        try
        {
            ChatSessions = currentUser is null
                ? Array.Empty<ChatSessionSummary>()
                : await _knowledge.GetSessionSummariesForOwnerAsync(currentUser.Id, cancellationToken);
        }
        catch (Exception ex) when (IsDataAccessTimeout(ex))
        {
            _logger.LogWarning(ex, "Chat page could not load sessions because the database was unavailable.");
            ChatSessions = Array.Empty<ChatSessionSummary>();
            LoadErrorMessage ??= "Database unavailable/timeout. Chat metadata could not be loaded.";
        }

        Documents = indexedDocuments;
        SubjectOptions = subjectOptions;
    }
}

