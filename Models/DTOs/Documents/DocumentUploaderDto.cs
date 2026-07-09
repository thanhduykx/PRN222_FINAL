namespace PRN222_FINAL.Models.DTOs.Documents;

public sealed record DocumentUploaderDto(
    Guid? UserId,
    string? Name,
    string? Email);
