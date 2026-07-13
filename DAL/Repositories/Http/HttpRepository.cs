using System.Text;
using System.Net;
using System.Net.Sockets;
using PRN222_FINAL.DAL.Models.Http;

namespace PRN222_FINAL.DAL.Repositories.Http;

public sealed class HttpRepository : IHttpRepository, IDisposable
{
    private readonly HttpClient _client;
    public HttpRepository(TimeSpan timeout) => _client = new HttpClient { Timeout = timeout };

    public async Task<HttpResponseData> SendAsync(HttpRequestData data, CancellationToken cancellationToken = default)
    {
        if (data.RejectPrivateNetworks)
        {
            return await SendRestrictedAsync(data, cancellationToken);
        }

        using var request = new HttpRequestMessage(new HttpMethod(data.Method), data.Url);
        if (data.Body is not null) request.Content = new StringContent(data.Body, Encoding.UTF8, data.ContentType);
        if (data.Headers is not null)
            foreach (var header in data.Headers) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        using var response = await _client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseData((int)response.StatusCode, response.ReasonPhrase ?? string.Empty, body)
        {
            FinalUrl = response.RequestMessage?.RequestUri?.AbsoluteUri
        };
    }

    private static async Task<HttpResponseData> SendRestrictedAsync(
        HttpRequestData data,
        CancellationToken cancellationToken)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectCallback = ConnectToPublicAddressAsync
        };
        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        var currentUri = RequireHttpUri(data.Url);
        var redirectCount = 0;
        while (true)
        {
            await EnsurePublicDestinationAsync(currentUri, cancellationToken);
            using var request = new HttpRequestMessage(new HttpMethod(data.Method), currentUri);
            if (data.Body is not null)
            {
                request.Content = new StringContent(data.Body, Encoding.UTF8, data.ContentType);
            }
            if (data.Headers is not null)
            {
                foreach (var header in data.Headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (IsRedirect(response.StatusCode) && response.Headers.Location is not null)
            {
                if (redirectCount++ >= Math.Clamp(data.MaxRedirects, 0, 10))
                {
                    throw new InvalidOperationException("The web page redirected too many times.");
                }

                currentUri = response.Headers.Location.IsAbsoluteUri
                    ? RequireHttpUri(response.Headers.Location.AbsoluteUri)
                    : RequireHttpUri(new Uri(currentUri, response.Headers.Location).AbsoluteUri);
                continue;
            }

            var body = await ReadLimitedBodyAsync(
                response.Content,
                Math.Clamp(data.MaxResponseBytes, 1, 10 * 1024 * 1024),
                cancellationToken);
            return new HttpResponseData((int)response.StatusCode, response.ReasonPhrase ?? string.Empty, body)
            {
                FinalUrl = currentUri.AbsoluteUri
            };
        }
    }

    private static async ValueTask<Stream> ConnectToPublicAddressAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        var address = addresses.FirstOrDefault(IsPublicAddress)
            ?? throw new InvalidOperationException("The destination resolves to a private or reserved network address.");
        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };
        try
        {
            await socket.ConnectAsync(address, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static async Task EnsurePublicDestinationAsync(Uri uri, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
        {
            throw new InvalidOperationException("Private, loopback, link-local and reserved destinations are not allowed.");
        }
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return false;
        }
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            return !address.IsIPv6LinkLocal
                   && !address.IsIPv6Multicast
                   && !address.IsIPv6SiteLocal
                   && (bytes[0] & 0xFE) != 0xFC;
        }

        var octets = address.GetAddressBytes();
        return octets[0] != 0
               && octets[0] != 10
               && octets[0] != 127
               && !(octets[0] == 100 && octets[1] is >= 64 and <= 127)
               && !(octets[0] == 169 && octets[1] == 254)
               && !(octets[0] == 172 && octets[1] is >= 16 and <= 31)
               && !(octets[0] == 192 && octets[1] == 168)
               && !(octets[0] == 198 && octets[1] is 18 or 19)
               && octets[0] < 224;
    }

    private static Uri RequireHttpUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException("A valid HTTP or HTTPS URL without user information is required.", nameof(value));
        }
        return uri;
    }

    private static bool IsRedirect(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.Moved or
        HttpStatusCode.Redirect or
        HttpStatusCode.RedirectMethod or
        HttpStatusCode.TemporaryRedirect or
        HttpStatusCode.PermanentRedirect;

    private static async Task<string> ReadLimitedBodyAsync(
        HttpContent content,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > 0 && content.Headers.ContentLength > maxBytes)
        {
            throw new InvalidOperationException($"The web response exceeds the {maxBytes / 1024 / 1024} MB limit.");
        }

        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        while (true)
        {
            var read = await source.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken);
            if (read == 0)
            {
                break;
            }
            if (buffer.Length + read > maxBytes)
            {
                throw new InvalidOperationException($"The web response exceeds the {maxBytes / 1024 / 1024} MB limit.");
            }
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    public void Dispose() => _client.Dispose();
}
