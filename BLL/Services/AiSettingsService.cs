using System.Text.Json;
using PRN222_FINAL.BLL;

namespace PRN222_FINAL.BLL.Services;

public sealed record AiSettings(string ChatModel, string EmbeddingModel, int EmbeddingDimensions,
    int ChunkSize, int ChunkOverlap);

public interface IAiSettingsService
{
    AiSettings Current { get; }
    Task SaveAsync(AiSettings settings, CancellationToken cancellationToken = default);
}

public sealed class AiSettingsService : IAiSettingsService
{
    private readonly string _settingsPath;
    private readonly GeminiOptions _gemini;
    private readonly FlmSyllabusAwareTextChunker _chunker;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly PRN222_FINAL.DAL.Repositories.Files.IFileRepository _files;

    public AiSettingsService(string contentRootPath, GeminiOptions gemini, FlmSyllabusAwareTextChunker chunker, PRN222_FINAL.DAL.Repositories.Files.IFileRepository files)
    {
        _gemini = gemini;
        _chunker = chunker;
        _files = files;
        var directory = Path.Combine(contentRootPath, "App_Data");

        _settingsPath = Path.Combine(directory, "ai-settings.json");
        LoadSavedSettings();
    }

    public AiSettings Current => new(_gemini.ChatModel, _gemini.EmbeddingModel,
        _gemini.EmbeddingDimensions, _chunker.ChunkSize, _chunker.Overlap);

    public async Task SaveAsync(AiSettings settings, CancellationToken cancellationToken = default)
    {
        var normalized = Validate(settings);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(normalized, new JsonSerializerOptions { WriteIndented = true });
            await _files.WriteTextAsync(_settingsPath, json, cancellationToken);
            Apply(normalized);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private void LoadSavedSettings()
    {
        if (!_files.Exists(_settingsPath)) return;
        try
        {
            var saved = JsonSerializer.Deserialize<AiSettings>(_files.ReadTextAsync(_settingsPath).GetAwaiter().GetResult());
            if (saved is not null) Apply(Validate(saved));
        }
        catch (Exception exception) when (exception is JsonException or IOException or ArgumentException)
        {
            throw new InvalidOperationException("Không thể đọc thiết lập trợ lý học tập đã lưu.", exception);
        }
    }

    private void Apply(AiSettings settings)
    {
        _gemini.ChatModel = settings.ChatModel;
        _gemini.EmbeddingModel = settings.EmbeddingModel;
        _gemini.EmbeddingDimensions = settings.EmbeddingDimensions;
        _chunker.Configure(settings.ChunkSize, settings.ChunkOverlap);
    }

    private static AiSettings Validate(AiSettings settings)
    {
        var chatModel = (settings.ChatModel ?? string.Empty).Trim();
        var embeddingModel = (settings.EmbeddingModel ?? string.Empty).Trim();
        if (chatModel.Length is < 3 or > 120 || embeddingModel.Length is < 3 or > 120)
            throw new ArgumentException("Tên mô hình phải có từ 3 đến 120 ký tự.");
        if (settings.EmbeddingDimensions is < 128 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(settings.EmbeddingDimensions), "Độ chi tiết dữ liệu phải từ 128 đến 4.096.");
        if (settings.ChunkSize is < 300 or > 4000)
            throw new ArgumentOutOfRangeException(nameof(settings.ChunkSize), "Độ dài mỗi đoạn phải từ 300 đến 4.000 ký tự.");
        if (settings.ChunkOverlap < 0 || settings.ChunkOverlap > Math.Min(500, settings.ChunkSize / 3))
            throw new ArgumentOutOfRangeException(nameof(settings.ChunkOverlap), "Phần nối không hợp lệ.");
        return settings with { ChatModel = chatModel, EmbeddingModel = embeddingModel };
    }
}
