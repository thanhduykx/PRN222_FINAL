using PRN222_FINAL.DAL.Models.Http;

namespace PRN222_FINAL.DAL.Repositories.Http;

public interface IHttpRepository
{
    Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken cancellationToken = default);
}
