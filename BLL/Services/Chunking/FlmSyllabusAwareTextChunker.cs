namespace PRN222_FINAL.BLL;

public sealed class FlmSyllabusAwareTextChunker : ITextChunker
{
    private int _chunkSize = 950;
    private int _overlap = 50;

    public int ChunkSize => Volatile.Read(ref _chunkSize);
    public int Overlap => Volatile.Read(ref _overlap);
    public string StrategyName => $"syllabus-aware-{ChunkSize}-{Overlap}-v2";

    public void Configure(int chunkSize, int overlap)
    {
        if (chunkSize is < 300 or > 4000)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSize), "Độ dài mỗi đoạn phải từ 300 đến 4.000 ký tự.");
        }

        if (overlap < 0 || overlap > Math.Min(500, chunkSize / 3))
        {
            throw new ArgumentOutOfRangeException(nameof(overlap), "Phần nối phải từ 0 đến 500 ký tự và không quá một phần ba độ dài đoạn.");
        }

        Volatile.Write(ref _chunkSize, chunkSize);
        Volatile.Write(ref _overlap, overlap);
    }

    public Task<TextChunkingResult> CreateChunkingResultAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(new TextChunkingResult(Array.Empty<TextChunk>()));
        }

        var chunkSize = ChunkSize;
        var overlap = Overlap;
        var chunks = new List<TextChunk>();

        for (var start = 0; start < text.Length; start += chunkSize - overlap)
        {
            var end = Math.Min(start + chunkSize, text.Length);
            var chunkText = text[start..end];

            chunks.Add(new TextChunk(
                chunks.Count,
                chunkText,
                string.Empty,
                start,
                end));
        }

        return Task.FromResult(new TextChunkingResult(chunks));
    }
}
