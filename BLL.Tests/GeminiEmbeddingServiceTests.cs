using System.Text.Json;
using PRN222_FINAL.BLL;
using PRN222_FINAL.DAL.Models.Http;
using PRN222_FINAL.DAL.Repositories.Http;
using Xunit;

namespace PRN222_FINAL.BLL.Tests;

public sealed class GeminiEmbeddingServiceTests
{
    [Theory]
    [InlineData(EmbeddingInputType.SearchQuery, "task: search result | query: authentication cookie")]
    [InlineData(EmbeddingInputType.Document, "title: none | text: authentication cookie")]
    public async Task EmbedAsync_UsesAsymmetricRetrievalFormat(
        EmbeddingInputType inputType,
        string expectedText)
    {
        var http = new CapturingEmbeddingHttpRepository();
        var service = new GeminiEmbeddingService(
            http,
            new GeminiOptions(
                true,
                "test-key",
                "gemini-3.5-flash",
                "gemini-embedding-2",
                768,
                30,
                "https://example.test/chat",
                "https://example.test/v1beta"));

        var embedding = await service.EmbedAsync(
            "  authentication   cookie  ",
            inputType);

        Assert.NotEmpty(embedding);
        Assert.Equal("POST", http.LastRequest?.Method);
        Assert.Equal(
            "https://example.test/v1beta/models/gemini-embedding-2:embedContent",
            http.LastRequest?.Url);
        Assert.Equal("test-key", http.LastRequest?.Headers?["x-goog-api-key"]);
        Assert.NotNull(http.LastRequest?.Body);
        using var request = JsonDocument.Parse(http.LastRequest!.Body!);
        Assert.Equal("models/gemini-embedding-2", request.RootElement.GetProperty("model").GetString());
        Assert.Equal(768, request.RootElement.GetProperty("outputDimensionality").GetInt32());
        var text = request.RootElement
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();
        Assert.Equal(expectedText, text);
    }

    private sealed class CapturingEmbeddingHttpRepository : IHttpRepository
    {
        public HttpRequestData? LastRequest { get; private set; }

        public Task<HttpResponseData> SendAsync(
            HttpRequestData request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseData(
                200,
                "OK",
                "{\"embedding\":{\"values\":[1.0,0.0]}}"));
        }
    }
}
