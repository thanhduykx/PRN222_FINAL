using B = PRN222_FINAL.BLL.Models;
using D = PRN222_FINAL.DAL.Models;

namespace PRN222_FINAL.BLL.Mapping;

public static class KnowledgeModelMapper
{
    public static D.DocumentAccessScope ToData(B.DocumentAccessScope x) => new(x.Role, x.UserId, x.Email, (D.DocumentAccessMode)x.Mode);
    public static D.DocumentListQuery? ToData(B.DocumentListQuery? x) => x is null ? null : new(x.Query, x.SubjectFilter, x.StatusFilter);
    public static D.SubjectOwnerInfo? ToData(B.SubjectOwnerInfo? x) => x is null ? null : new(x.UserId, x.Name, x.Email);
    public static D.ChatSessionOwnerInfo? ToData(B.ChatSessionOwnerInfo? x) => x is null ? null : new(x.UserId, x.Name, x.Email);

    public static B.IndexedDocument ToModel(D.IndexedDocument x) => new()
    { Id=x.Id,FileName=x.FileName,StoredPath=x.StoredPath,Subject=x.Subject,Chapter=x.Chapter,ContentType=x.ContentType,UploadedAt=x.UploadedAt,ChunkCount=x.ChunkCount,FileSizeBytes=x.FileSizeBytes,UploadedByUserId=x.UploadedByUserId,UploadedByName=x.UploadedByName,UploadedByEmail=x.UploadedByEmail,Status=x.Status,IndexedAt=x.IndexedAt,IndexError=x.IndexError,EmbeddingModel=x.EmbeddingModel,EmbeddingDimensions=x.EmbeddingDimensions,ChunkingStrategy=x.ChunkingStrategy };
    public static D.IndexedDocument ToData(B.IndexedDocument x) => new()
    { Id=x.Id,FileName=x.FileName,StoredPath=x.StoredPath,Subject=x.Subject,Chapter=x.Chapter,ContentType=x.ContentType,UploadedAt=x.UploadedAt,ChunkCount=x.ChunkCount,FileSizeBytes=x.FileSizeBytes,UploadedByUserId=x.UploadedByUserId,UploadedByName=x.UploadedByName,UploadedByEmail=x.UploadedByEmail,Status=x.Status,IndexedAt=x.IndexedAt,IndexError=x.IndexError,EmbeddingModel=x.EmbeddingModel,EmbeddingDimensions=x.EmbeddingDimensions,ChunkingStrategy=x.ChunkingStrategy };
    public static B.DocumentChunk ToModel(D.DocumentChunk x) => new()
    { Id=x.Id,DocumentId=x.DocumentId,FileName=x.FileName,Subject=x.Subject,Chapter=x.Chapter,ChunkIndex=x.ChunkIndex,Text=x.Text,SectionTitle=x.SectionTitle,CharStart=x.CharStart,CharEnd=x.CharEnd,Embedding=new(x.Embedding) };
    public static D.DocumentChunk ToData(B.DocumentChunk x) => new()
    { Id=x.Id,DocumentId=x.DocumentId,FileName=x.FileName,Subject=x.Subject,Chapter=x.Chapter,ChunkIndex=x.ChunkIndex,Text=x.Text,SectionTitle=x.SectionTitle,CharStart=x.CharStart,CharEnd=x.CharEnd,Embedding=new(x.Embedding) };
    public static B.CourseChapter ToModel(D.CourseChapter x) => new() { Id=x.Id,SubjectId=x.SubjectId,SubjectCode=x.SubjectCode,SubjectName=x.SubjectName,Title=x.Title,SortOrder=x.SortOrder };
    public static B.CourseSubject ToModel(D.CourseSubject x) => new() { Id=x.Id,Code=x.Code,Name=x.Name,Description=x.Description,CreatedAt=x.CreatedAt,IsActive=x.IsActive,OwnerUserId=x.OwnerUserId,OwnerName=x.OwnerName,OwnerEmail=x.OwnerEmail,StudentCount=x.StudentCount,Chapters=x.Chapters.Select(ToModel).ToList() };
    public static B.SourceCitation ToModel(D.SourceCitation x) => new() { DocumentId=x.DocumentId,FileName=x.FileName,Subject=x.Subject,Chapter=x.Chapter,ChunkIndex=x.ChunkIndex,Score=x.Score,Excerpt=x.Excerpt };
    public static D.SourceCitation ToData(B.SourceCitation x) => new() { DocumentId=x.DocumentId,FileName=x.FileName,Subject=x.Subject,Chapter=x.Chapter,ChunkIndex=x.ChunkIndex,Score=x.Score,Excerpt=x.Excerpt };
    public static B.ChatMessage ToModel(D.ChatMessage x) => new() { Role=x.Role,Content=x.Content,CreatedAt=x.CreatedAt,Citations=x.Citations.Select(ToModel).ToList() };
    public static D.ChatMessage ToData(B.ChatMessage x) => new() { Role=x.Role,Content=x.Content,CreatedAt=x.CreatedAt,Citations=x.Citations.Select(ToData).ToList() };
    public static B.ChatSession ToModel(D.ChatSession x) => new() { Id=x.Id,Title=x.Title,IsStarred=x.IsStarred,CreatedAt=x.CreatedAt,UpdatedAt=x.UpdatedAt,OwnerUserId=x.OwnerUserId,OwnerName=x.OwnerName,OwnerEmail=x.OwnerEmail,Messages=x.Messages.Select(ToModel).ToList() };
    public static B.ChatSessionSummary ToModel(D.ChatSessionSummary x) => new() { Id=x.Id,Title=x.Title,IsStarred=x.IsStarred,CreatedAt=x.CreatedAt,UpdatedAt=x.UpdatedAt,MessageCount=x.MessageCount,FirstUserMessagePreview=x.FirstUserMessagePreview };
}
