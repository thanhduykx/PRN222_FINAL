namespace PRN222_FINAL.DAL.Models;

public sealed class KnowledgeStore
{
    public List<IndexedDocument> Documents { get; set; } = new();
    public List<DocumentChunk> Chunks { get; set; } = new();
    public List<ChatSession> Sessions { get; set; } = new();
    public List<CourseSubject> CourseSubjects { get; set; } = new();
}
