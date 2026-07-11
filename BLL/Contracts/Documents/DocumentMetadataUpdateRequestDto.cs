namespace PRN222_FINAL.BLL.Contracts.Documents;

public sealed record DocumentMetadataUpdateRequestDto(
    Guid DocumentId,
    string FileName,
    string Subject,
    string Chapter);
