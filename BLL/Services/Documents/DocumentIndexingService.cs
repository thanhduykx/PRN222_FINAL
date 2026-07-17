using System.Text;
using PRN222_FINAL.BLL.Models;
using PRN222_FINAL.BLL.Contracts.Documents;
using PRN222_FINAL.BLL.Mapping;
using PRN222_FINAL.DAL.Repositories;
using PRN222_FINAL.DAL.Repositories.Files;

namespace PRN222_FINAL.BLL;

public sealed record DocumentIndexingProgressUpdate(
    Guid DocumentId,
    string FileName,
    string Subject,
    string Chapter,
    string Status,
    string Stage,
    int ProgressPercent,
    string Message);

public interface IDocumentIndexingService
{
    Task<IReadOnlyList<DocumentDto>> GetDocumentsAsync(CancellationToken cancellationToken = default);
    Task<DocumentUploadResultDto> QueueFileAsync(
        DocumentFileUploadRequestDto request,
        CancellationToken cancellationToken = default);
    Task<DocumentUploadResultDto> QueueTextAsync(
        DocumentTextUploadRequestDto request,
        CancellationToken cancellationToken = default);
    Task ProcessDocumentAsync(
        Guid documentId,
        IProgress<DocumentIndexingProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed class DocumentIndexingService : IDocumentIndexingService
{
    private readonly IKnowledgeRepository _repository;
    private readonly IDocumentTextExtractor _extractor;
    private readonly IEmbeddingService _embeddingService;
    private readonly ITextChunker _chunker;
    private readonly IChunkRetrievalEnrichmentService _chunkEnrichment;
    private readonly IFileRepository _files;

    public DocumentIndexingService(
        IKnowledgeRepository repository,
        IDocumentTextExtractor extractor,
        IEmbeddingService embeddingService,
        ITextChunker chunker,
        IChunkRetrievalEnrichmentService chunkEnrichment,
        IFileRepository files)
    {
        _repository = repository;
        _extractor = extractor;
        _embeddingService = embeddingService;
        _chunker = chunker;
        _chunkEnrichment = chunkEnrichment;
        _files = files;
    }

    private string EffectiveChunkingStrategy => $"{_chunker.StrategyName}+{_chunkEnrichment.StrategyName}";

    public async Task<IReadOnlyList<DocumentDto>> GetDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var documents = await _repository.GetDocumentsAsync(cancellationToken);
        return documents.Select(KnowledgeModelMapper.ToModel).Select(DocumentDtoMapper.ToDto).ToList();
    }

    public async Task<DocumentUploadResultDto> QueueFileAsync(
        DocumentFileUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var uploadsRoot = NormalizeRequiredText(request.UploadsRoot, "Upload path is required.");
        await using var copy = new MemoryStream();
        await request.FileStream.CopyToAsync(copy, cancellationToken);
        if (copy.Length == 0)
        {
            throw new InvalidOperationException("The selected file is empty and cannot be indexed.");
        }

        var safeFileName = NormalizeFileName(request.FileName);
        var storedPath = Path.Combine(uploadsRoot, $"{Guid.NewGuid():N}{Path.GetExtension(safeFileName)}");
        copy.Position = 0;
        var savedLength = await _files.SaveAsync(storedPath, copy, cancellationToken);

        var document = CreateProcessingDocument(
            safeFileName,
            request.ContentType,
            request.Subject,
            request.Chapter,
            storedPath,
            savedLength,
            request.Uploader);

        await _repository.AddDocumentAsync(KnowledgeModelMapper.ToData(document), Array.Empty<PRN222_FINAL.DAL.Models.DocumentChunk>(), cancellationToken);
        return new DocumentUploadResultDto(document.Id, 0, $"Queued {document.FileName} for indexing.");
    }

    public async Task<DocumentUploadResultDto> QueueTextAsync(
        DocumentTextUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            throw new InvalidOperationException("No readable text could be extracted from this source.");
        }

        var uploadsRoot = NormalizeRequiredText(request.UploadsRoot, "Upload path is required.");
        var safeSourceName = MakeSafeSourceName(request.SourceName);
        var storedPath = Path.Combine(uploadsRoot, $"{Guid.NewGuid():N}.txt");
        var normalizedText = TextEncodingHelper.NormalizeForIndexing(request.Text);
        await _files.WriteTextAsync(storedPath, normalizedText, cancellationToken);

        var document = CreateProcessingDocument(
            safeSourceName,
            request.ContentType,
            request.Subject,
            request.Chapter,
            storedPath,
            Encoding.UTF8.GetByteCount(normalizedText),
            request.Uploader);

        await _repository.AddDocumentAsync(KnowledgeModelMapper.ToData(document), Array.Empty<PRN222_FINAL.DAL.Models.DocumentChunk>(), cancellationToken);
        return new DocumentUploadResultDto(document.Id, 0, $"Queued {document.FileName} for indexing.");
    }
    public async Task ProcessDocumentAsync(
        Guid documentId,
        IProgress<DocumentIndexingProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var documentData = await _repository.GetDocumentAsync(documentId, cancellationToken);
        var document = documentData is null ? null : KnowledgeModelMapper.ToModel(documentData);
        if (document is null)
        {
            return;
        }

        if (document.Status == DocumentIndexStatus.Indexed
            && document.ChunkCount > 0
            && document.EmbeddingModel.Equals(_embeddingService.ModelName, StringComparison.Ordinal)
            && document.EmbeddingDimensions == _embeddingService.Dimensions
            && document.ChunkingStrategy.Equals(EffectiveChunkingStrategy, StringComparison.Ordinal))
        {
            return;
        }

        void ReportProgress(string stage, int progressPercent, string message)
        {
            progress?.Report(new DocumentIndexingProgressUpdate(
                document.Id,
                document.FileName,
                document.Subject,
                document.Chapter,
                DocumentIndexStatus.Processing,
                stage,
                Math.Clamp(progressPercent, 0, 100),
                message));
        }

        ReportProgress("Queued", 5, "Queued for indexing.");
        await _repository.MarkDocumentIndexProcessingAsync(document.Id, cancellationToken);

        var storedPath = Path.GetFullPath(document.StoredPath);
        if (!_files.Exists(storedPath))
        {
            throw new InvalidOperationException("Stored file was not found for indexing.");
        }

        ReportProgress("Extracting", 18, "Extracting readable text from the source file.");
        string extractedText;
        await using (var stream = await _files.OpenReadAsync(storedPath, cancellationToken))
        {
            extractedText = TextEncodingHelper.NormalizeForIndexing(await _extractor.ExtractAsync(stream, document.FileName, cancellationToken));
        }

        if (string.IsNullOrWhiteSpace(extractedText))
        {
            throw new InvalidOperationException("No readable text could be extracted from this document.");
        }

        ReportProgress("Chunking", 42, "Creating searchable chunks from extracted content.");
        var chunkingResult = await _chunker.CreateChunkingResultAsync(extractedText, cancellationToken);
        var chunkTexts = chunkingResult.Chunks;
        if (chunkTexts.Count == 0)
        {
            throw new InvalidOperationException("No indexable chunks could be created from this document.");
        }

        ReportProgress("Embedding", 55, $"Generating embeddings for {chunkTexts.Count} chunks.");
        var chunks = new List<DocumentChunk>(chunkTexts.Count);
        var progressStep = Math.Max(1, chunkTexts.Count / 8);
        foreach (var chunk in chunkTexts)
        {
            var embeddingInput = await _chunkEnrichment.BuildEmbeddingTextAsync(
                chunk,
                new ChunkRetrievalEnrichmentContext(
                    document.FileName,
                    document.Subject,
                    document.Chapter,
                    chunk.SectionTitle),
                cancellationToken);

            chunks.Add(new DocumentChunk
            {
                DocumentId = document.Id,
                FileName = document.FileName,
                Subject = document.Subject,
                Chapter = document.Chapter,
                // Chunk indexes are ordered within a document. They must not
                // use the database-wide maximum, otherwise every newly
                // indexed file starts after the previous file's last chunk.
                ChunkIndex = chunk.ChunkIndex,
                Text = chunk.Text,
                SectionTitle = chunk.SectionTitle,
                CharStart = chunk.CharStart,
                CharEnd = chunk.CharEnd,
                Embedding = await _embeddingService.EmbedAsync(
                    embeddingInput.EmbeddingText,
                    EmbeddingInputType.Document,
                    cancellationToken)
            });

            if (chunk.ChunkIndex == chunkTexts.Count || chunk.ChunkIndex % progressStep == 0)
            {
                var chunkProgress = 55 + (int)Math.Round((double)chunk.ChunkIndex / chunkTexts.Count * 30);
                ReportProgress("Embedding", chunkProgress, $"Embedding chunk {chunk.ChunkIndex + 1}/{chunkTexts.Count}.");
            }
        }

        ReportProgress("Saving", 92, "Saving indexed chunks and metadata.");
        await _repository.CompleteDocumentIndexAsync(
            document.Id,
            chunks.Select(KnowledgeModelMapper.ToData).ToList(),
            _embeddingService.ModelName,
            _embeddingService.Dimensions,
            EffectiveChunkingStrategy,
            cancellationToken);
        ReportProgress("Completed", 100, $"Index completed with {chunks.Count} chunks.");
    }

    private IndexedDocument CreateProcessingDocument(
        string fileName,
        string contentType,
        string subject,
        string chapter,
        string storedPath,
        long fileSizeBytes,
        DocumentUploaderDto uploader)
    {
        return new IndexedDocument
        {
            FileName = NormalizeFileName(fileName),
            Subject = NormalizeRequiredText(subject, "Subject is required."),
            Chapter = NormalizeRequiredText(chapter, "Chapter is required."),
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim(),
            StoredPath = storedPath,
            UploadedAt = DateTimeOffset.UtcNow,
            FileSizeBytes = fileSizeBytes,
            UploadedByUserId = uploader.UserId,
            UploadedByName = uploader.Name?.Trim() ?? string.Empty,
            UploadedByEmail = uploader.Email?.Trim() ?? string.Empty,
            Status = DocumentIndexStatus.Processing,
            ChunkCount = 0,
            IndexedAt = null,
            IndexError = string.Empty,
            EmbeddingModel = _embeddingService.ModelName,
            EmbeddingDimensions = _embeddingService.Dimensions,
            ChunkingStrategy = EffectiveChunkingStrategy
        };
    }

    private static string NormalizeFileName(string fileName)
    {
        var normalized = Path.GetFileName((fileName ?? string.Empty).Trim());
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("File name is required.");
        }

        return normalized;
    }

    private static string NormalizeRequiredText(string value, string errorMessage)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return normalized;
    }

    private static string MakeSafeSourceName(string sourceName)
    {
        var name = string.IsNullOrWhiteSpace(sourceName) ? "web-page.txt" : sourceName.Trim();
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidCharacter, '-');
        }

        if (string.IsNullOrWhiteSpace(Path.GetExtension(name)))
        {
            name += ".txt";
        }

        return name.Length <= 120 ? name : name[..120];
    }
}

