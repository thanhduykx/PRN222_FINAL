using PRN222_FINAL.BLL.Services.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_FINAL.BLL;
using PRN222_FINAL.BLL.Models;
using PRN222_FINAL.BLL.Services.Documents;
using PRN222_FINAL.Web.Security;
using PRN222_FINAL.Web.Services;

namespace PRN222_FINAL.Web.Pages.Home;

[Authorize(Policy = AuthorizationPolicies.DocumentManagement)]
public sealed class ViewDocumentModel : HomePageModelBase
{
    private readonly IDocumentFileService _documentFiles;

    public ViewDocumentModel(
        ILogger<HomePageModelBase> logger,
        IKnowledgeService knowledge,
        IDocumentIndexingService indexingService,
        IWebPageTextExtractor webPageTextExtractor,
        IRagChatService chatService,
        IUserAccountService users,
        IWebHostEnvironment environment,
        IDocumentIndexJobQueue indexJobQueue,
        IDocumentFileService documentFiles)
        : base(logger, knowledge, indexingService, webPageTextExtractor, chatService, users, environment, indexJobQueue)
    {
        _documentFiles = documentFiles;
    }

    public IndexedDocument Document { get; private set; } = new();
    public new string Content { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await _knowledge.GetDocumentAsync(id, cancellationToken);
        if (document is null) return NotFound();
        if (!await CanViewDocumentAsync(document, cancellationToken)) return Forbid();

        if (IsTextDocument(document))
        {
            var text = await _documentFiles.ReadTextAsync(document.StoredPath, GetUploadsRoot(), cancellationToken);
            if (text is null) return NotFound();
            Document = document;
            Content = text;
            return Page();
        }

        var stream = await _documentFiles.OpenReadAsync(document.StoredPath, GetUploadsRoot(), cancellationToken);
        if (stream is null) return NotFound();
        Response.Headers.ContentDisposition = $"inline; filename=\"{document.FileName.Replace("\"", string.Empty)}\"";
        return new FileStreamResult(stream, ResolveContentType(document)) { EnableRangeProcessing = true };
    }
}
