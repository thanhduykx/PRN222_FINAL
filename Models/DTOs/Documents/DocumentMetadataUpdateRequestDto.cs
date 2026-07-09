namespace PRN222_FINAL.Models.DTOs.Documents;

public sealed record DocumentMetadataUpdateRequestDto(
    Guid DocumentId,
    string FileName,
    string Subject,
    string Chapter);
