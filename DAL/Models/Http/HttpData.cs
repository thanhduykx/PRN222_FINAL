namespace PRN222_FINAL.DAL.Models.Http;

public sealed record HttpRequestData(string Method, string Url, string? Body = null,
    string ContentType = "application/json", IReadOnlyDictionary<string, string>? Headers = null);

public sealed record HttpResponseData(int StatusCode, string ReasonPhrase, string Body)
{
    public bool IsSuccessStatusCode => StatusCode is >= 200 and <= 299;
}
