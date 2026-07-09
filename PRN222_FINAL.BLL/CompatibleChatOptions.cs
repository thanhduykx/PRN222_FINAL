namespace PRN222_FINAL.BLL;

public sealed record CompatibleChatOptions(
    bool Enabled,
    string ApiKey,
    string Model,
    int TimeoutSeconds,
    string BaseUrl);

