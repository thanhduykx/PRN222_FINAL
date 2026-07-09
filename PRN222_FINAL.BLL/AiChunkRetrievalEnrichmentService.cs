namespace PRN222_FINAL.BLL;

public sealed class AiChunkRetrievalEnrichmentService : IChunkRetrievalEnrichmentService
{
    public string StrategyName => "ai-enrichment-v1";

    public Task<ChunkRetrievalEnrichmentResult> BuildEmbeddingTextAsync(
        TextChunk chunk,
        ChunkRetrievalEnrichmentContext context,
        CancellationToken cancellationToken = default)
    {
        var text = $"{context.Subject} {context.Chapter} {context.SectionTitle} {chunk.Text}".Trim();
        return Task.FromResult(new ChunkRetrievalEnrichmentResult(text, false, StrategyName));
    }
}
