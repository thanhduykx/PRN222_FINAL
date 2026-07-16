using PRN222_FINAL.BLL.Services.Accounts;
using PRN222_FINAL.BLL.Security;
using System.Text.RegularExpressions;
using PRN222_FINAL.BLL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_FINAL.Web.Security;
using PRN222_FINAL.Web.Services;
using PRN222_FINAL.BLL;
using PRN222_FINAL.BLL.Services.Analytics;
using PRN222_FINAL.BLL.Contracts.Analytics;

namespace PRN222_FINAL.Web.Pages.Home;

[Authorize(Policy = AuthorizationPolicies.ChatAccess)]
public sealed class CourseWorkspaceModel : HomePageModelBase
{
    private static readonly Regex SentenceRegex = new(
        @"(?<=[.!?。])\s+|\r?\n+",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));
    private readonly IAnalyticsService _analytics;
    private readonly IDocumentStatusNotifier _documentStatusNotifier;

    public CourseWorkspaceModel(
        ILogger<CourseWorkspaceModel> logger,
        IKnowledgeService knowledge,
        IDocumentIndexingService indexingService,
        IWebPageTextExtractor webPageTextExtractor,
        IRagChatService chatService,
        IUserAccountService users,
        IWebHostEnvironment environment,
        IDocumentIndexJobQueue indexJobQueue,
        IDocumentStatusNotifier documentStatusNotifier,
        IAnalyticsService analytics)
        : base(logger, knowledge, indexingService, webPageTextExtractor, chatService, users, environment, indexJobQueue)
    {
        _documentStatusNotifier = documentStatusNotifier;
        _analytics = analytics;
    }

    public CourseSubject Subject { get; private set; } = new();
    public IReadOnlyList<IndexedDocument> Documents { get; private set; } = Array.Empty<IndexedDocument>();
    public IReadOnlyList<CourseChapterLearningViewModel> Chapters { get; private set; } = Array.Empty<CourseChapterLearningViewModel>();
    public bool CanManageCourse { get; private set; }
    public string? LoadErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var role = CurrentRole();
            if (role == AppRoles.Student)
            {
                return Forbid();
            }

            var documentScope = BuildDocumentAccessScope(DocumentAccessMode.DocumentUi);
            var documents = await _knowledge.GetDocumentsAsync(documentScope, null, cancellationToken);
            var catalog = await _knowledge.GetCourseCatalogAsync(cancellationToken);
            var visibleCatalog = BuildSynchronizedCourseCatalogForView(FilterCourseCatalogForCurrentUser(catalog), documents);
            var subject = visibleCatalog.FirstOrDefault(item => item.Id == id);
            if (subject is null)
            {
                return NotFound();
            }

            var subjectDocuments = documents
                .Where(document => SubjectMatchesFilter(document.Subject, subject.DisplayName)
                                   || SubjectMatchesFilter(document.Subject, subject.Code))
                .OrderBy(document => document.Chapter)
                .ThenByDescending(document => document.UploadedAt)
                .ToList();
            var subjectLabels = subjectDocuments
                .Select(document => document.Subject)
                .Append(subject.DisplayName)
                .Append(subject.Code)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var chunks = await _knowledge.GetChunksAsync(documentScope, subjectLabels, cancellationToken);
            chunks = chunks
                .Where(chunk => SubjectMatchesFilter(chunk.Subject, subject.DisplayName)
                                || SubjectMatchesFilter(chunk.Subject, subject.Code))
                .OrderBy(chunk => chunk.Chapter)
                .ThenBy(chunk => chunk.ChunkIndex)
                .ToList();

            Subject = subject;
            Documents = subjectDocuments;
            Chapters = BuildChapterLearning(subject, subjectDocuments, chunks);
            CanManageCourse = await CanManageSubjectAsync(subject.Id, cancellationToken);
            await TrackCourseAccessAsync(subject, "workspace", cancellationToken);

            return Page();
        }
        catch (Exception ex) when (IsDataAccessTimeout(ex))
        {
            _logger.LogWarning(ex, "Course workspace could not load because the database was unavailable.");
            LoadErrorMessage = "Database unavailable/timeout. Course workspace could not be loaded.";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteDocumentAsync(Guid id, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await _knowledge.GetDocumentAsync(documentId, cancellationToken);
        if (document is null)
        {
            TempData["Error"] = "Không tìm thấy tài liệu để xóa.";
            return RedirectToPage(new { id });
        }

        if (!await CanManageDocumentAsync(document, cancellationToken))
        {
            return Forbid();
        }

        await _knowledge.DeleteDocumentAsync(documentId, GetUploadsRoot(), cancellationToken);
        TempData["Success"] = $"Đã xóa tài liệu {document.FileName}.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReindexDocumentAsync(Guid id, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await _knowledge.GetDocumentAsync(documentId, cancellationToken);
        if (document is null)
        {
            TempData["Error"] = "Không tìm thấy tài liệu để re-index.";
            return RedirectToPage(new { id });
        }

        if (!await CanManageDocumentAsync(document, cancellationToken))
        {
            return Forbid();
        }

        await _knowledge.MarkDocumentIndexProcessingAsync(documentId, cancellationToken);
        await _documentStatusNotifier.NotifyDocumentStatusChangedAsync(documentId, CancellationToken.None);
        await _indexJobQueue.EnqueueAsync(documentId, cancellationToken);
        TempData["Success"] = $"Đã đưa {document.FileName} vào hàng đợi re-index.";
        return RedirectToPage(new { id });
    }

    private static IReadOnlyList<CourseChapterLearningViewModel> BuildChapterLearning(
        CourseSubject subject,
        IReadOnlyList<IndexedDocument> documents,
        IReadOnlyList<DocumentChunk> chunks)
    {
        var chapterNames = subject.Chapters
            .Select(chapter => chapter.Title)
            .Concat(documents.Select(document => document.Chapter))
            .Concat(chunks.Select(chunk => chunk.Chapter))
            .Where(chapter => !string.IsNullOrWhiteSpace(chapter))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(chapter => chapter)
            .ToList();

        return chapterNames.Select(chapter => new CourseChapterLearningViewModel(
            chapter,
            documents.Count(document => document.Chapter.Equals(chapter, StringComparison.OrdinalIgnoreCase)),
            chunks.Count(chunk => chunk.Chapter.Equals(chapter, StringComparison.OrdinalIgnoreCase)),
            FirstUsefulSentence(chunks.FirstOrDefault(chunk => chunk.Chapter.Equals(chapter, StringComparison.OrdinalIgnoreCase))?.Text) ?? "No indexed summary yet."))
            .ToList();
    }

    private static string? FirstUsefulSentence(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return SentenceRegex.Split(text.Trim())
            .Select(sentence => sentence.Trim())
            .FirstOrDefault(sentence => sentence.Length >= 35) is { } found
            ? TrimTo(found, 180)
            : TrimTo(text.Trim(), 180);
    }

    private static string TrimTo(string value, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength].TrimEnd() + "...";
    }

    private async Task TrackCourseAccessAsync(CourseSubject subject, string accessArea, CancellationToken cancellationToken)
    {
        try
        {
            await _analytics.TrackCourseAccessAsync(new CourseAccessLogRequestDto
            {
                UserId = CurrentUserId(),
                UserName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty,
                UserEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? string.Empty,
                Role = CurrentRole(),
                SubjectId = subject.Id,
                SubjectCode = subject.Code,
                SubjectName = subject.DisplayName,
                AccessArea = accessArea
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not track course access for subject {SubjectId}", subject.Id);
        }
    }

}

public sealed record CourseChapterLearningViewModel(string Title, int DocumentCount, int ChunkCount, string Summary);

