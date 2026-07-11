namespace PRN222_FINAL.BLL;

public static class ChatProviders
{
    public const string Gemini = "Gemini";
    public const string Groq = "Groq";

    public static string Normalize(string? provider) =>
        provider?.Trim().Equals(Groq, StringComparison.OrdinalIgnoreCase) == true ? Groq : Gemini;
}

public sealed class ChatGenerationOptions
{
    private readonly object _selectionLock = new();
    private string _provider = ChatProviders.Gemini;
    private string _model = "gemini-3.5-flash";

    public string Provider
    {
        get { lock (_selectionLock) return _provider; }
        set { lock (_selectionLock) _provider = ChatProviders.Normalize(value); }
    }

    public string Model
    {
        get { lock (_selectionLock) return _model; }
        set { lock (_selectionLock) _model = value?.Trim() ?? string.Empty; }
    }
    public bool GeminiEnabled { get; init; }
    public string GeminiApiKey { get; init; } = string.Empty;
    public string GeminiBaseUrl { get; init; } = string.Empty;
    public bool GroqEnabled { get; init; }
    public string GroqApiKey { get; init; } = string.Empty;
    public string GroqBaseUrl { get; init; } = "https://api.groq.com/openai/v1/chat/completions";
    public int TimeoutSeconds { get; init; } = 60;

    public (string Provider, string Model) CurrentSelection
    {
        get { lock (_selectionLock) return (_provider, _model); }
    }

    public void Configure(string provider, string model)
    {
        lock (_selectionLock)
        {
            _provider = ChatProviders.Normalize(provider);
            _model = model?.Trim() ?? string.Empty;
        }
    }

    public CompatibleChatOptions CurrentCompatibleOptions
    {
        get
        {
            lock (_selectionLock)
            {
                return _provider == ChatProviders.Groq
                    ? new CompatibleChatOptions(GroqEnabled, GroqApiKey, _model, TimeoutSeconds, GroqBaseUrl)
                    : new CompatibleChatOptions(GeminiEnabled, GeminiApiKey, _model, TimeoutSeconds, GeminiBaseUrl);
            }
        }
    }
}
