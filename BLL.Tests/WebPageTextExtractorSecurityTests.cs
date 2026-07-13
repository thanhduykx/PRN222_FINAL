using PRN222_FINAL.BLL;
using PRN222_FINAL.DAL.Repositories.Http;
using Xunit;

namespace PRN222_FINAL.BLL.Tests;

public sealed class WebPageTextExtractorSecurityTests
{
    [Theory]
    [InlineData("http://127.0.0.1/admin")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://[::1]/")]
    public async Task ExtractAsync_RejectsPrivateAndMetadataDestinations(string url)
    {
        using var http = new HttpRepository(TimeSpan.FromSeconds(2));
        var extractor = new WebPageTextExtractor(http);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => extractor.ExtractAsync(url));

        Assert.Contains("not allowed", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
