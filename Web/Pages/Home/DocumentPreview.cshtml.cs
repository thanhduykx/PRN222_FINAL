using PRN222_FINAL.BLL.Services.Accounts;
using PRN222_FINAL.BLL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_FINAL.Web.Models;
using PRN222_FINAL.Web.Security;
using PRN222_FINAL.Web.Services;
using PRN222_FINAL.BLL;

namespace PRN222_FINAL.Web.Pages.Home;

[Authorize(Policy = AuthorizationPolicies.DocumentManagement)]
public sealed class DocumentPreviewModel : HomePageModelBase
{
    public DocumentPreviewModel(
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

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await _knowledge.GetDocumentAsync(id, cancellationToken);
        if (document is null)
        {
            return NotFound(new { error = "Document not found." });
        }

        if (!await CanViewDocumentAsync(document, cancellationToken))
        {
            return Forbid();
        }

        if (document.Status != DocumentIndexStatus.Indexed)
        {
            return BadRequest(new
            {
                error = string.IsNullOrWhiteSpace(document.IndexError)
                    ? "Document is not indexed yet."
                    : document.IndexError,
                status = document.Status
            });
        }

        var subjectOwner = await ResolveSubjectOwnerAsync(document.Subject, cancellationToken);
        var chunks = await _knowledge.GetDocumentChunksAsync(document.Id, cancellationToken);
        var preview = new DocumentPreviewViewModel
        {
            Id = document.Id,
            FileName = document.FileName,
            Subject = document.Subject,
            Chapter = document.Chapter,
            ContentType = document.ContentType,
            UploadedAt = document.UploadedAt,
            ChunkCount = document.ChunkCount,
            FileSizeBytes = document.FileSizeBytes,
            UploadedByName = document.UploadedByName,
            UploadedByEmail = document.UploadedByEmail,
            Status = document.Status,
            IndexedAt = document.IndexedAt,
            IndexError = document.IndexError,
            EmbeddingModel = document.EmbeddingModel,
            EmbeddingDimensions = document.EmbeddingDimensions,
            ChunkingStrategy = document.ChunkingStrategy,
            SubjectOwnerName = subjectOwner.Name,
            SubjectOwnerEmail = subjectOwner.Email,
            Chunks = chunks
                .Select(chunk => new DocumentPreviewChunkViewModel
                {
                    ChunkIndex = chunk.ChunkIndex,
                    SectionTitle = chunk.SectionTitle,
                    CharStart = chunk.CharStart,
                    CharEnd = chunk.CharEnd,
                    Text = chunk.Text
                })
                .ToList()
        };

        return new JsonResult(preview);
    }
}

