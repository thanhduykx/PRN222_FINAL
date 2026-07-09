namespace PRN222_FINAL.Models.DTOs.Documents;

public sealed class DocumentTextUploadRequestDto
{
    public string Text { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Chapter { get; init; } = string.Empty;
    public string UploadsRoot { get; init; } = string.Empty;
    public DocumentUploaderDto Uploader { get; init; } = new(null, null, null);
}
