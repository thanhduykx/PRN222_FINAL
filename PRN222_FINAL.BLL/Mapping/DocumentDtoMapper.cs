using PRN222_FINAL.Models;
using PRN222_FINAL.Models.DTOs.Documents;

namespace PRN222_FINAL.BLL;

internal static class DocumentDtoMapper
{
    public static DocumentDto ToDto(IndexedDocument document)
    {
        return new DocumentDto
        {
            Id = document.Id,
            FileName = document.FileName,
            StoredPath = document.StoredPath,
            Subject = document.Subject,
            Chapter = document.Chapter,
            ContentType = document.ContentType,
            UploadedAt = document.UploadedAt,
            ChunkCount = document.ChunkCount,
            FileSizeBytes = document.FileSizeBytes,
            UploadedByUserId = document.UploadedByUserId,
            UploadedByName = document.UploadedByName,
            UploadedByEmail = document.UploadedByEmail,
            Status = document.Status,
            IndexedAt = document.IndexedAt,
            IndexError = document.IndexError,
            EmbeddingModel = document.EmbeddingModel,
            EmbeddingDimensions = document.EmbeddingDimensions,
            ChunkingStrategy = document.ChunkingStrategy
        };
    }

    public static DocumentChunkDto ToDto(DocumentChunk chunk)
    {
        return new DocumentChunkDto
        {
            Id = chunk.Id,
            DocumentId = chunk.DocumentId,
            FileName = chunk.FileName,
            Subject = chunk.Subject,
            Chapter = chunk.Chapter,
            ChunkIndex = chunk.ChunkIndex,
            Text = chunk.Text,
            SectionTitle = chunk.SectionTitle,
            CharStart = chunk.CharStart,
            CharEnd = chunk.CharEnd
        };
    }
}
