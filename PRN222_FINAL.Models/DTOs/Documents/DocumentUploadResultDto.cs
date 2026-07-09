namespace PRN222_FINAL.Models.DTOs.Documents;

public sealed record DocumentUploadResultDto(
    Guid DocumentId,
    int ChunkCount,
    string Message);
