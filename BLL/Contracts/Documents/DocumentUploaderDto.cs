namespace PRN222_FINAL.BLL.Contracts.Documents;

public sealed record DocumentUploaderDto(
    Guid? UserId,
    string? Name,
    string? Email);
