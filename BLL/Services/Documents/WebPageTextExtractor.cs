using PRN222_FINAL.DAL.Models.Http;
using PRN222_FINAL.DAL.Repositories.Http;

namespace PRN222_FINAL.BLL;

public interface IWebPageTextExtractor
{
    Task<WebPageExtractionResult> ExtractAsync(string url, CancellationToken cancellationToken = default);
}

public sealed record WebPageExtractionResult(string Title, string SourceUrl, string Text)
{
    public bool UsedBrowserRenderer { get; init; }
}

public sealed class WebPageTextExtractor : IWebPageTextExtractor
{
    private readonly IHttpRepository _http;
    public WebPageTextExtractor(IHttpRepository http) => _http = http;

    public async Task<WebPageExtractionResult> ExtractAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("A valid HTTP or HTTPS URL is required.", nameof(url));
        var response = await _http.SendAsync(new HttpRequestData("GET", uri.ToString())
        {
            RejectPrivateNetworks = true,
            MaxResponseBytes = 5 * 1024 * 1024,
            MaxRedirects = 5
        }, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Web request failed with HTTP {response.StatusCode}. {response.ReasonPhrase}");
        return new WebPageExtractionResult("Web Page", response.FinalUrl ?? uri.ToString(), response.Body);
    }
}
