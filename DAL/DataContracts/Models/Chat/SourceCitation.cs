namespace PRN222_FINAL.DAL.Models;

public sealed class SourceCitation
{
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Chapter { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public double Score { get; set; }
    public string Excerpt { get; set; } = string.Empty;
}
