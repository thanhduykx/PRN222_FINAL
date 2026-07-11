namespace PRN222_FINAL.BLL.Contracts.Documents;

public sealed class DocumentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Chapter { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; }
    public int ChunkCount { get; set; }
    public long FileSizeBytes { get; set; }
    public Guid? UploadedByUserId { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public string UploadedByEmail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? IndexedAt { get; set; }
    public string IndexError { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = string.Empty;
    public int EmbeddingDimensions { get; set; }
    public string ChunkingStrategy { get; set; } = string.Empty;
}
