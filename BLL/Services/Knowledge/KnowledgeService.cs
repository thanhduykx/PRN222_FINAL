using PRN222_FINAL.BLL.Models;
using PRN222_FINAL.DAL.Repositories;
using PRN222_FINAL.BLL.Mapping;
using PRN222_FINAL.DAL.Repositories.Files;

namespace PRN222_FINAL.BLL;

public interface IKnowledgeService
{
    Task<IReadOnlyList<IndexedDocument>> GetDocumentsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IndexedDocument>> GetDocumentsAsync(
        DocumentAccessScope scope,
        DocumentListQuery? query = null,
        CancellationToken cancellationToken = default);
    Task<IndexedDocument?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IndexedDocument>> GetDocumentsByStatusAsync(string status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentChunk>> GetChunksAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentChunk>> GetChunksAsync(
        DocumentAccessScope scope,
        IReadOnlyCollection<string>? allowedSubjects = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentChunk>> GetDocumentChunksAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetIndexedSubjectsAsync(DocumentAccessScope scope, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetStaleIndexedDocumentIdsAsync(
        string embeddingModel,
        int embeddingDimensions,
        string? chunkingStrategy,
        DocumentAccessScope scope,
        CancellationToken cancellationToken = default);
    Task AddDocumentAsync(IndexedDocument document, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default);
    Task MarkDocumentIndexProcessingAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task CompleteDocumentIndexAsync(
        Guid documentId,
        IReadOnlyList<DocumentChunk> chunks,
        string embeddingModel,
        int embeddingDimensions,
        string chunkingStrategy,
        CancellationToken cancellationToken = default);
    Task MarkDocumentIndexFailedAsync(Guid documentId, string errorMessage, CancellationToken cancellationToken = default);
    Task<IndexedDocument> UpdateDocumentMetadataAsync(Guid documentId, string fileName, string subject, string chapter, CancellationToken cancellationToken = default);
    Task DeleteDocumentAsync(Guid documentId, string uploadsRoot, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CourseSubject>> GetCourseCatalogAsync(CancellationToken cancellationToken = default);
    Task<CourseSubject> UpsertSubjectAsync(Guid? subjectId, string code, string name, string? description, CancellationToken cancellationToken = default, SubjectOwnerInfo? ownerInfo = null);
    Task<CourseChapter> UpsertChapterAsync(Guid? chapterId, Guid subjectId, string title, int sortOrder, CancellationToken cancellationToken = default);
    Task DeleteChapterAsync(Guid chapterId, CancellationToken cancellationToken = default);
    Task RemoveSubjectLecturerAsync(Guid subjectId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetSubjectStudentIdsAsync(Guid subjectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetSubjectStudentAssignmentsAsync(CancellationToken cancellationToken = default);
    Task AddSubjectStudentAsync(Guid subjectId, Guid userId, CancellationToken cancellationToken = default);
    Task RemoveSubjectStudentAsync(Guid subjectId, Guid userId, CancellationToken cancellationToken = default);
    Task SetSubjectActiveStatusAsync(Guid subjectId, bool isActive, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatSession>> GetSessionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatSession>> GetSessionsForOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatSessionSummary>> GetSessionSummariesForOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default);
    Task<ChatSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<ChatSession?> GetSessionForOwnerAsync(Guid sessionId, Guid ownerUserId, CancellationToken cancellationToken = default);
    Task<ChatSession> GetOrCreateSessionAsync(Guid sessionId, CancellationToken cancellationToken = default, ChatSessionOwnerInfo? ownerInfo = null);
    Task<ChatSession?> RenameSessionAsync(Guid sessionId, string title, CancellationToken cancellationToken = default, ChatSessionOwnerInfo? ownerInfo = null);
    Task<ChatSession?> SetSessionStarredAsync(Guid sessionId, bool isStarred, CancellationToken cancellationToken = default, ChatSessionOwnerInfo? ownerInfo = null);
    Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default, ChatSessionOwnerInfo? ownerInfo = null);
    Task AddMessageAsync(Guid sessionId, ChatMessage message, CancellationToken cancellationToken = default, ChatSessionOwnerInfo? ownerInfo = null);
}

public sealed class KnowledgeService : IKnowledgeService
{
    private readonly IKnowledgeRepository _repository;
    private readonly IFileRepository _files;

    public KnowledgeService(IKnowledgeRepository repository, IFileRepository files)
    {
        _repository = repository;
        _files = files;
    }

    public async Task<IReadOnlyList<IndexedDocument>> GetDocumentsAsync(CancellationToken cancellationToken = default) => (await _repository.GetDocumentsAsync(cancellationToken)).Select(KnowledgeModelMapper.ToModel).ToList();
    public async Task<IReadOnlyList<IndexedDocument>> GetDocumentsAsync(DocumentAccessScope scope, DocumentListQuery? query = null, CancellationToken cancellationToken = default) => (await _repository.GetDocumentsAsync(KnowledgeModelMapper.ToData(scope), KnowledgeModelMapper.ToData(query), cancellationToken)).Select(KnowledgeModelMapper.ToModel).ToList();
    public async Task<IndexedDocument?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default) { var x=await _repository.GetDocumentAsync(documentId,cancellationToken); return x is null?null:KnowledgeModelMapper.ToModel(x); }
    public async Task<IReadOnlyList<IndexedDocument>> GetDocumentsByStatusAsync(string status, CancellationToken cancellationToken = default) => (await _repository.GetDocumentsByStatusAsync(status,cancellationToken)).Select(KnowledgeModelMapper.ToModel).ToList();
    public async Task<IReadOnlyList<DocumentChunk>> GetChunksAsync(CancellationToken cancellationToken = default) => (await _repository.GetChunksAsync(cancellationToken)).Select(KnowledgeModelMapper.ToModel).ToList();
    public async Task<IReadOnlyList<DocumentChunk>> GetChunksAsync(DocumentAccessScope scope, IReadOnlyCollection<string>? allowedSubjects = null, CancellationToken cancellationToken = default) => (await _repository.GetChunksAsync(KnowledgeModelMapper.ToData(scope),allowedSubjects,cancellationToken)).Select(KnowledgeModelMapper.ToModel).ToList();
    public async Task<IReadOnlyList<DocumentChunk>> GetDocumentChunksAsync(Guid documentId, CancellationToken cancellationToken = default) => (await _repository.GetDocumentChunksAsync(documentId,cancellationToken)).Select(KnowledgeModelMapper.ToModel).ToList();
    public Task<IReadOnlyList<string>> GetIndexedSubjectsAsync(DocumentAccessScope scope, CancellationToken cancellationToken = default) => _repository.GetIndexedSubjectsAsync(KnowledgeModelMapper.ToData(scope), cancellationToken);
    public Task<IReadOnlyList<Guid>> GetStaleIndexedDocumentIdsAsync(string embeddingModel, int embeddingDimensions, string? chunkingStrategy, DocumentAccessScope scope, CancellationToken cancellationToken = default) => _repository.GetStaleIndexedDocumentIdsAsync(embeddingModel, embeddingDimensions, chunkingStrategy, KnowledgeModelMapper.ToData(scope), cancellationToken);
    public Task AddDocumentAsync(IndexedDocument document, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default) => _repository.AddDocumentAsync(KnowledgeModelMapper.ToData(document), chunks.Select(KnowledgeModelMapper.ToData).ToList(), cancellationToken);
    public Task MarkDocumentIndexProcessingAsync(Guid documentId, CancellationToken cancellationToken = default) => _repository.MarkDocumentIndexProcessingAsync(documentId, cancellationToken);
    public Task CompleteDocumentIndexAsync(Guid documentId, IReadOnlyList<DocumentChunk> chunks, string embeddingModel, int embeddingDimensions, string chunkingStrategy, CancellationToken cancellationToken = default) => _repository.CompleteDocumentIndexAsync(documentId, chunks.Select(KnowledgeModelMapper.ToData).ToList(), embeddingModel, embeddingDimensions, chunkingStrategy, cancellationToken);
    public Task MarkDocumentIndexFailedAsync(Guid documentId, string errorMessage, CancellationToken cancellationToken = default) => _repository.MarkDocumentIndexFailedAsync(documentId, errorMessage, cancellationToken);
    public async Task<IndexedDocument> UpdateDocumentMetadataAsync(Guid documentId, string fileName, string subject, string chapter, CancellationToken cancellationToken = default) => KnowledgeModelMapper.ToModel(await _repository.UpdateDocumentMetadataAsync(documentId, fileName, subject, chapter, cancellationToken));
    public async Task DeleteDocumentAsync(Guid documentId, string uploadsRoot, CancellationToken cancellationToken = default)
    {
        var document = await _repository.GetDocumentAsync(documentId, cancellationToken);
        await _repository.DeleteDocumentAsync(documentId, cancellationToken);
        if (document is null || string.IsNullOrWhiteSpace(document.StoredPath)) return;
        var path = Path.GetFullPath(document.StoredPath);
        var root = Path.GetFullPath(uploadsRoot);
        if (!root.EndsWith(Path.DirectorySeparatorChar)) root += Path.DirectorySeparatorChar;
        if (path.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            _files.Delete(path);
    }
    public async Task<IReadOnlyList<CourseSubject>> GetCourseCatalogAsync(CancellationToken cancellationToken = default) => (await _repository.GetCourseCatalogAsync(cancellationToken)).Select(KnowledgeModelMapper.ToModel).ToList();
    public async Task<CourseSubject> UpsertSubjectAsync(Guid? subjectId, string code, string name, string? description, CancellationToken cancellationToken = default, SubjectOwnerInfo? ownerInfo = null) => KnowledgeModelMapper.ToModel(await _repository.UpsertSubjectAsync(subjectId, code, name, description, cancellationToken, KnowledgeModelMapper.ToData(ownerInfo)));
    public async Task<CourseChapter> UpsertChapterAsync(Guid? chapterId, Guid subjectId, string title, int sortOrder, CancellationToken cancellationToken = default) => KnowledgeModelMapper.ToModel(await _repository.UpsertChapterAsync(chapterId, subjectId, title, sortOrder, cancellationToken));
    public Task DeleteChapterAsync(Guid chapterId, CancellationToken cancellationToken = default) => _repository.DeleteChapterAsync(chapterId, cancellationToken);
    public Task RemoveSubjectLecturerAsync(Guid subjectId, Guid userId, CancellationToken cancellationToken = default) => _repository.RemoveSubjectLecturerAsync(subjectId, userId, cancellationToken);
    public Task<IReadOnlyList<Guid>> GetSubjectStudentIdsAsync(Guid subjectId, CancellationToken cancellationToken = default) => _repository.GetSubjectStudentIdsAsync(subjectId, cancellationToken);
    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetSubjectStudentAssignmentsAsync(CancellationToken cancellationToken = default) => _repository.GetSubjectStudentAssignmentsAsync(cancellationToken);
    public Task AddSubjectStudentAsync(Guid subjectId, Guid userId, CancellationToken cancellationToken = default) => _repository.AddSubjectStudentAsync(subjectId, userId, cancellationToken);
    public Task RemoveSubjectStudentAsync(Guid subjectId, Guid userId, CancellationToken cancellationToken = default) => _repository.RemoveSubjectStudentAsync(subjectId, userId, cancellationToken);
    public Task SetSubjectActiveStatusAsync(Guid subjectId, bool isActive, CancellationToken cancellationToken = default) => _repository.SetSubjectActiveStatusAsync(subjectId, isActive, cancellationToken);
    public async Task<IReadOnlyList<ChatSession>> GetSessionsAsync(CancellationToken cancellationToken = default) => (await _repository.GetSessionsAsync(cancellationToken)).Select(KnowledgeModelMapper.ToModel).ToList();
    public async Task<IReadOnlyList<ChatSession>> GetSessionsForOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default) => (await _repository.GetSessionsForOwnerAsync(ownerUserId,cancellationToken)).Select(KnowledgeModelMapper.ToModel).ToList();
    public async Task<IReadOnlyList<ChatSessionSummary>> GetSessionSummariesForOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default) => (await _repository.GetSessionSummariesForOwnerAsync(ownerUserId,cancellationToken)).Select(KnowledgeModelMapper.ToModel).ToList();
    public async Task<ChatSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default) {var x=await _repository.GetSessionAsync(sessionId,cancellationToken);return x is null?null:KnowledgeModelMapper.ToModel(x);}
    public async Task<ChatSession?> GetSessionForOwnerAsync(Guid sessionId, Guid ownerUserId, CancellationToken cancellationToken = default) {var x=await _repository.GetSessionForOwnerAsync(sessionId,ownerUserId,cancellationToken);return x is null?null:KnowledgeModelMapper.ToModel(x);}
    public async Task<ChatSession> GetOrCreateSessionAsync(Guid sessionId, CancellationToken cancellationToken = default, ChatSessionOwnerInfo? ownerInfo = null) => KnowledgeModelMapper.ToModel(await _repository.GetOrCreateSessionAsync(sessionId,cancellationToken,KnowledgeModelMapper.ToData(ownerInfo)));
    public async Task<ChatSession?> RenameSessionAsync(Guid sessionId, string title, CancellationToken cancellationToken = default, ChatSessionOwnerInfo? ownerInfo = null) {var x=await _repository.RenameSessionAsync(sessionId,title,cancellationToken,KnowledgeModelMapper.ToData(ownerInfo));return x is null?null:KnowledgeModelMapper.ToModel(x);}
    public async Task<ChatSession?> SetSessionStarredAsync(Guid sessionId, bool isStarred, CancellationToken cancellationToken = default, ChatSessionOwnerInfo? ownerInfo = null) {var x=await _repository.SetSessionStarredAsync(sessionId,isStarred,cancellationToken,KnowledgeModelMapper.ToData(ownerInfo));return x is null?null:KnowledgeModelMapper.ToModel(x);}
    public Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default, ChatSessionOwnerInfo? ownerInfo = null) => _repository.DeleteSessionAsync(sessionId,cancellationToken,KnowledgeModelMapper.ToData(ownerInfo));
    public Task AddMessageAsync(Guid sessionId, ChatMessage message, CancellationToken cancellationToken = default, ChatSessionOwnerInfo? ownerInfo = null) => _repository.AddMessageAsync(sessionId,KnowledgeModelMapper.ToData(message),cancellationToken,KnowledgeModelMapper.ToData(ownerInfo));
}

