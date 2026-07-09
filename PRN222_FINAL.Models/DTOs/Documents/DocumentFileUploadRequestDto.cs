namespace PRN222_FINAL.Models.DTOs.Documents;

public sealed class DocumentFileUploadRequestDto
{
    public Stream FileStream { get; init; } = Stream.Null;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Chapter { get; init; } = string.Empty;
    public string UploadsRoot { get; init; } = string.Empty;
    public DocumentUploaderDto Uploader { get; init; } = new(null, null, null);
}
