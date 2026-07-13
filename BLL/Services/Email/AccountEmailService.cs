using System.Net;
using PRN222_FINAL.BLL.Models;

namespace PRN222_FINAL.BLL.Services.Email;

public interface IAccountEmailService
{
    Task SendPasswordResetEmailAsync(
        UserAccount account,
        string resetUrl,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    Task SendWelcomeActivationEmailAsync(
        UserAccount account,
        string activationUrl,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);
}

public sealed class AccountEmailService : IAccountEmailService
{
    private readonly PRN222_FINAL.DAL.Repositories.Email.IEmailRepository _email;

    public AccountEmailService(PRN222_FINAL.DAL.Repositories.Email.IEmailRepository email)
    {
        _email = email;
    }

    public Task SendPasswordResetEmailAsync(
        UserAccount account,
        string resetUrl,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default) =>
        SendSetPasswordEmailAsync(account, resetUrl, expiresAt, isActivation: false, cancellationToken);

    public Task SendWelcomeActivationEmailAsync(
        UserAccount account,
        string activationUrl,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default) =>
        SendSetPasswordEmailAsync(account, activationUrl, expiresAt, isActivation: true, cancellationToken);

    private Task SendSetPasswordEmailAsync(
        UserAccount account,
        string url,
        DateTimeOffset expiresAt,
        bool isActivation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsedUrl)
            || (!parsedUrl.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !parsedUrl.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("A valid account action URL is required.");
        }

        var subject = isActivation
            ? "Kích hoạt tài khoản Course Assistant"
            : "Đặt lại mật khẩu Course Assistant";
        return _email.SendAsync(new PRN222_FINAL.DAL.Models.Email.EmailMessageData(
            account.Email,
            account.FullName,
            subject,
            BuildPlainText(account, parsedUrl.AbsoluteUri, expiresAt, isActivation),
            BuildHtml(account, parsedUrl.AbsoluteUri, expiresAt, isActivation)),
            cancellationToken);
    }

    private static string BuildPlainText(
        UserAccount account,
        string actionUrl,
        DateTimeOffset expiresAt,
        bool isActivation)
    {
        var heading = isActivation ? "Kích hoạt tài khoản" : "Đặt lại mật khẩu";
        var explanation = isActivation
            ? $"Tài khoản {account.Email} đã được nhà trường tạo. Hãy đặt mật khẩu của riêng bạn để kích hoạt tài khoản."
            : $"Hệ thống nhận được yêu cầu đặt lại mật khẩu cho tài khoản {account.Email}.";
        var expiryText = expiresAt.ToLocalTime().ToString("HH:mm dd/MM/yyyy");
        return $"""
            {heading} Course Assistant

            Xin chào {account.FullName},

            {explanation}

            Liên kết bảo mật dùng một lần:
            {actionUrl}

            Liên kết hết hạn lúc {expiryText}. Không chuyển tiếp email này cho người khác.

            Trân trọng,
            CPMS
            """;
    }

    private static string BuildHtml(
        UserAccount account,
        string actionUrl,
        DateTimeOffset expiresAt,
        bool isActivation)
    {
        var fullName = WebUtility.HtmlEncode(account.FullName);
        var email = WebUtility.HtmlEncode(account.Email);
        var safeUrl = WebUtility.HtmlEncode(actionUrl);
        var heading = isActivation ? "Kích hoạt tài khoản" : "Đặt lại mật khẩu";
        var button = isActivation ? "Đặt mật khẩu và kích hoạt" : "Đặt lại mật khẩu";
        var explanation = isActivation
            ? $"Tài khoản <strong>{email}</strong> đã được nhà trường tạo. Hãy đặt mật khẩu của riêng bạn để kích hoạt tài khoản."
            : $"Hệ thống nhận được yêu cầu đặt lại mật khẩu cho tài khoản <strong>{email}</strong>.";
        var expiryText = WebUtility.HtmlEncode(expiresAt.ToLocalTime().ToString("HH:mm dd/MM/yyyy"));

        return $$"""
            <!doctype html>
            <html lang="vi">
            <body style="margin:0;background:#f4f7fb;font-family:Arial,Helvetica,sans-serif;color:#102033;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="padding:28px 12px;">
                <tr><td align="center">
                  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:620px;background:#fff;border:1px solid #dbe4ef;border-radius:12px;">
                    <tr><td style="padding:28px;">
                      <h1 style="margin:0 0 14px;color:#0f4c81;font-size:24px;">{{heading}}</h1>
                      <p style="font-size:16px;line-height:1.6;">Xin chào <strong>{{fullName}}</strong>,</p>
                      <p style="font-size:15px;line-height:1.6;">{{explanation}}</p>
                      <p style="margin:22px 0;"><a href="{{safeUrl}}" style="display:inline-block;background:#0f4c81;color:#fff;text-decoration:none;padding:12px 18px;border-radius:8px;font-weight:700;">{{button}}</a></p>
                      <p style="font-size:14px;color:#64748b;">Liên kết dùng một lần và hết hạn lúc <strong>{{expiryText}}</strong>.</p>
                      <p style="font-size:13px;color:#64748b;">Không chuyển tiếp email này cho người khác.</p>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }
}
