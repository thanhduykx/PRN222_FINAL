namespace PRN222_FINAL.BLL;

public sealed class GeminiOptions
{
    public GeminiOptions(bool enabled, string apiKey, string chatModel, string embeddingModel,
        int embeddingDimensions, int timeoutSeconds, string chatBaseUrl, string embeddingBaseUrl)
    {
        Enabled = enabled;
        ApiKey = apiKey;
        ChatModel = chatModel;
        EmbeddingModel = embeddingModel;
        EmbeddingDimensions = embeddingDimensions;
        TimeoutSeconds = timeoutSeconds;
        ChatBaseUrl = chatBaseUrl;
        EmbeddingBaseUrl = embeddingBaseUrl;
    }

    public bool Enabled { get; set; }
    public string ApiKey { get; set; }
    public string ChatModel { get; set; }
    public string EmbeddingModel { get; set; }
    public int EmbeddingDimensions { get; set; }
    public int TimeoutSeconds { get; set; }
    public string ChatBaseUrl { get; set; }
    public string EmbeddingBaseUrl { get; set; }
}

