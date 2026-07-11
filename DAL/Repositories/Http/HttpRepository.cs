using System.Text;
using PRN222_FINAL.DAL.Models.Http;

namespace PRN222_FINAL.DAL.Repositories.Http;

public sealed class HttpRepository : IHttpRepository, IDisposable
{
    private readonly HttpClient _client;
    public HttpRepository(TimeSpan timeout) => _client = new HttpClient { Timeout = timeout };

    public async Task<HttpResponseData> SendAsync(HttpRequestData data, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(new HttpMethod(data.Method), data.Url);
        if (data.Body is not null) request.Content = new StringContent(data.Body, Encoding.UTF8, data.ContentType);
        if (data.Headers is not null)
            foreach (var header in data.Headers) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        using var response = await _client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseData((int)response.StatusCode, response.ReasonPhrase ?? string.Empty, body);
    }

    public void Dispose() => _client.Dispose();
}
