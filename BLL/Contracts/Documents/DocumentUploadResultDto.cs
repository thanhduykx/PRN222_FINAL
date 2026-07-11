namespace PRN222_FINAL.BLL.Contracts.Documents;

public sealed record DocumentUploadResultDto(
    Guid DocumentId,
    int ChunkCount,
    string Message);
