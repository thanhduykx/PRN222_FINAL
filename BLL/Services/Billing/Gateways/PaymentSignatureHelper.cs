using System.Security.Cryptography;
using System.Text;

namespace PRN222_FINAL.BLL.Services.Billing.Gateways;

internal static class PaymentSignatureHelper
{
    public static string HmacSha256(string data, string secretKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secretKey ?? string.Empty);
        var dataBytes = Encoding.UTF8.GetBytes(data ?? string.Empty);
        using var hmac = new HMACSHA256(keyBytes);
        return Convert.ToHexString(hmac.ComputeHash(dataBytes)).ToLowerInvariant();
    }

    public static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left ?? string.Empty);
        var rightBytes = Encoding.UTF8.GetBytes(right ?? string.Empty);
        return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
