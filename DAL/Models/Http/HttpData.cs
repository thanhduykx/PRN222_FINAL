namespace PRN222_FINAL.DAL.Models.Http;

public sealed record HttpRequestData(string Method, string Url, string? Body = null,
    string ContentType = "application/json", IReadOnlyDictionary<string, string>? Headers = null)
{
    public bool RejectPrivateNetworks { get; init; }
    public int MaxResponseBytes { get; init; } = 5 * 1024 * 1024;
    public int MaxRedirects { get; init; } = 5;
}

public sealed record HttpResponseData(int StatusCode, string ReasonPhrase, string Body)
{
    public bool IsSuccessStatusCode => StatusCode is >= 200 and <= 299;
    public string? FinalUrl { get; init; }
}
