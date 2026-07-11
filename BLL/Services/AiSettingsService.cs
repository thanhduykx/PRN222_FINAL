using System.Text.Json;
using PRN222_FINAL.BLL;

namespace PRN222_FINAL.BLL.Services;

public sealed record AiSettings(string ChatModel, string EmbeddingModel, int EmbeddingDimensions,
    int ChunkSize, int ChunkOverlap, string ChatProvider = ChatProviders.Gemini);

public interface IAiSettingsService
{
    AiSettings Current { get; }
    IReadOnlyList<string> SupportedChatProviders { get; }
    IReadOnlyList<string> SupportedChatModels { get; }
    IReadOnlyDictionary<string, IReadOnlyList<string>> SupportedChatModelsByProvider { get; }
    IReadOnlyList<string> SupportedEmbeddingModels { get; }
    bool IsChatProviderConfigured(string provider);
    Task SaveAsync(AiSettings settings, CancellationToken cancellationToken = default);
}

public sealed class AiSettingsService : IAiSettingsService
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ChatModelsByProvider =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [ChatProviders.Gemini] = ["gemini-3.5-flash", "gemini-2.5-flash", "gemini-2.5-pro"],
            [ChatProviders.Groq] = ["llama-3.3-70b-versatile", "llama-3.1-8b-instant", "openai/gpt-oss-20b"]
        };

    private static readonly string[] EmbeddingModels =
    [
        "gemini-embedding-2",
        "gemini-embedding-001",
        "text-embedding-004"
    ];

    private readonly string _settingsPath;
    private readonly GeminiOptions _gemini;
    private readonly ChatGenerationOptions _chatGeneration;
    private readonly FlmSyllabusAwareTextChunker _chunker;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly PRN222_FINAL.DAL.Repositories.Files.IFileRepository _files;

    public AiSettingsService(string contentRootPath, GeminiOptions gemini, ChatGenerationOptions chatGeneration,
        FlmSyllabusAwareTextChunker chunker, PRN222_FINAL.DAL.Repositories.Files.IFileRepository files)
    {
        _gemini = gemini;
        _chatGeneration = chatGeneration;
        _chunker = chunker;
        _files = files;
        var directory = Path.Combine(contentRootPath, "App_Data");

        _settingsPath = Path.Combine(directory, "ai-settings.json");
        LoadSavedSettings();
    }

    public AiSettings Current
    {
        get
        {
            var selection = _chatGeneration.CurrentSelection;
            return new AiSettings(selection.Model, _gemini.EmbeddingModel,
                _gemini.EmbeddingDimensions, _chunker.ChunkSize, _chunker.Overlap, selection.Provider);
        }
    }
    public IReadOnlyList<string> SupportedChatProviders => [ChatProviders.Gemini, ChatProviders.Groq];
    public IReadOnlyList<string> SupportedChatModels => ChatModelsByProvider.Values.SelectMany(models => models).ToArray();
    public IReadOnlyDictionary<string, IReadOnlyList<string>> SupportedChatModelsByProvider => ChatModelsByProvider;
    public IReadOnlyList<string> SupportedEmbeddingModels => EmbeddingModels;

    public bool IsChatProviderConfigured(string provider)
    {
        var normalized = ChatProviders.Normalize(provider);
        var options = normalized == ChatProviders.Groq
            ? new CompatibleChatOptions(_chatGeneration.GroqEnabled, _chatGeneration.GroqApiKey, string.Empty, 0, string.Empty)
            : new CompatibleChatOptions(_chatGeneration.GeminiEnabled, _chatGeneration.GeminiApiKey, string.Empty, 0, string.Empty);
        return options.Enabled && !string.IsNullOrWhiteSpace(options.ApiKey);
    }

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
        _chatGeneration.Configure(settings.ChatProvider, settings.ChatModel);
        if (settings.ChatProvider == ChatProviders.Gemini)
        {
            _gemini.ChatModel = settings.ChatModel;
        }
        _gemini.EmbeddingModel = settings.EmbeddingModel;
        _gemini.EmbeddingDimensions = settings.EmbeddingDimensions;
        _chunker.Configure(settings.ChunkSize, settings.ChunkOverlap);
    }

    private static AiSettings Validate(AiSettings settings)
    {
        var chatProvider = ChatProviders.Normalize(settings.ChatProvider);
        var chatModel = (settings.ChatModel ?? string.Empty).Trim();
        var embeddingModel = (settings.EmbeddingModel ?? string.Empty).Trim();
        if (!ChatModelsByProvider.TryGetValue(chatProvider, out var supportedModels)
            || !supportedModels.Contains(chatModel, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Model trả lời không thuộc nhà cung cấp đã chọn.");
        if (!EmbeddingModels.Contains(embeddingModel, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Model đọc tài liệu không nằm trong danh sách được hỗ trợ.");
        if (settings.EmbeddingDimensions is < 128 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(settings.EmbeddingDimensions), "Độ chi tiết dữ liệu phải từ 128 đến 4.096.");
        if (settings.ChunkSize is < 300 or > 4000)
            throw new ArgumentOutOfRangeException(nameof(settings.ChunkSize), "Độ dài mỗi đoạn phải từ 300 đến 4.000 ký tự.");
        if (settings.ChunkOverlap < 0 || settings.ChunkOverlap > Math.Min(500, settings.ChunkSize / 3))
            throw new ArgumentOutOfRangeException(nameof(settings.ChunkOverlap), "Phần nối không hợp lệ.");
        return settings with { ChatProvider = chatProvider, ChatModel = chatModel, EmbeddingModel = embeddingModel };
    }
}
