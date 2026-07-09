using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using PRN222_FINAL.Web.Models;
using PRN222_FINAL.Web.Security;

namespace PRN222_FINAL.Web.Services;

public sealed record SmtpOptions(
    string Host,
    int Port,
    bool EnableSsl,
    string FromEmail,
    string FromName,
    string UserName,
    string Password);

public interface IAccountEmailSender
{
    Task SendWelcomeEmailAsync(
        UserAccount account,
        string temporaryPassword,
        string? loginUrl,
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? subjectLabels = null);

    Task SendPasswordResetEmailAsync(
        UserAccount account,
        string resetUrl,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);
}

public sealed class SmtpAccountEmailSender : IAccountEmailSender
{
    private const string LogoContentId = "fpt-logo";

    private readonly SmtpOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SmtpAccountEmailSender> _logger;

    public SmtpAccountEmailSender(
        SmtpOptions options,
        IWebHostEnvironment environment,
        ILogger<SmtpAccountEmailSender> logger)
    {
        _options = options;
        _environment = environment;
        _logger = logger;
    }

    public async Task SendWelcomeEmailAsync(
        UserAccount account,
        string temporaryPassword,
        string? loginUrl,
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? subjectLabels = null)
    {
        ValidateOptions();

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName, Encoding.UTF8),
            Subject = "ChÃ o má»«ng báº¡n tá»›i á»¨ng dá»¥ng Chat Bot - Quáº£n LÃ½ TÃ i Liá»‡u",
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8
        };
        message.To.Add(new MailAddress(account.Email, account.FullName, Encoding.UTF8));

        var plainText = BuildPlainText(account, temporaryPassword, loginUrl, subjectLabels);
        var html = BuildHtml(account, temporaryPassword, loginUrl, subjectLabels);
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(plainText, Encoding.UTF8, MediaTypeNames.Text.Plain));

        var htmlView = AlternateView.CreateAlternateViewFromString(html, Encoding.UTF8, MediaTypeNames.Text.Html);
        var logoPath = Path.Combine(_environment.WebRootPath, "img", "fptlogo.png");
        if (File.Exists(logoPath))
        {
            var logo = new LinkedResource(logoPath, "image/png")
            {
                ContentId = LogoContentId,
                TransferEncoding = TransferEncoding.Base64
            };
            logo.ContentType.Name = "fptlogo.png";
            htmlView.LinkedResources.Add(logo);
        }
        else
        {
            _logger.LogWarning("Welcome email logo was not found at {LogoPath}", logoPath);
        }

        message.AlternateViews.Add(htmlView);

        await SendAsync(message, cancellationToken);
    }

    public async Task SendPasswordResetEmailAsync(
        UserAccount account,
        string resetUrl,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions();

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName, Encoding.UTF8),
            Subject = "Äáº·t láº¡i máº­t kháº©u Course Assistant",
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8
        };
        message.To.Add(new MailAddress(account.Email, account.FullName, Encoding.UTF8));

        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            BuildPasswordResetPlainText(account, resetUrl, expiresAt),
            Encoding.UTF8,
            MediaTypeNames.Text.Plain));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            BuildPasswordResetHtml(account, resetUrl, expiresAt),
            Encoding.UTF8,
            MediaTypeNames.Text.Html));

        await SendAsync(message, cancellationToken);
    }

    private async Task SendAsync(MailMessage message, CancellationToken cancellationToken)
    {
        var password = NormalizeCredentialPassword(_options.Host, _options.Password);
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            Credentials = new NetworkCredential(_options.UserName, password),
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        await client.SendMailAsync(message, cancellationToken);
    }

    private static string NormalizeCredentialPassword(string host, string password)
    {
        if (!host.Equals("smtp.gmail.com", StringComparison.OrdinalIgnoreCase))
        {
            return password;
        }

        var compactPassword = string.Concat(password.Where(character => !char.IsWhiteSpace(character)));
        return compactPassword.Length == 16 && compactPassword.All(char.IsLetterOrDigit)
            ? compactPassword
            : password;
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.Host)
            || string.IsNullOrWhiteSpace(_options.FromEmail)
            || string.IsNullOrWhiteSpace(_options.UserName)
            || string.IsNullOrWhiteSpace(_options.Password))
        {
            throw new InvalidOperationException("SMTP configuration is incomplete.");
        }
    }

    private static string BuildPlainText(
        UserAccount account,
        string temporaryPassword,
        string? loginUrl,
        IReadOnlyList<string>? subjectLabels)
    {
        var loginLine = string.IsNullOrWhiteSpace(loginUrl)
            ? "ÄÄƒng nháº­p táº¡i Ä‘á»‹a chá»‰ á»©ng dá»¥ng Ä‘Æ°á»£c nhÃ  trÆ°á»ng cung cáº¥p."
            : $"ÄÄƒng nháº­p: {loginUrl}";
        var accessSummary = BuildAccessSummary(account, subjectLabels);

        return $"""
            ChÃ o má»«ng báº¡n tá»›i á»¨ng dá»¥ng Chat Bot - Quáº£n LÃ½ TÃ i Liá»‡u

            Xin chÃ o {account.FullName},

            TÃ i khoáº£n cá»§a báº¡n Ä‘Ã£ Ä‘Æ°á»£c táº¡o thÃ nh cÃ´ng.

            Email Ä‘Äƒng nháº­p: {account.Email}
            Vai trÃ²: {account.Role}
            MÃ´n / quyá»n truy cáº­p: {accessSummary}
            Máº­t kháº©u táº¡m thá»i: {temporaryPassword}

            {loginLine}

            LÆ°u Ã½ báº£o máº­t tÃ i khoáº£n:
            - KhÃ´ng chia sáº» máº­t kháº©u cho báº¥t ká»³ ai.
            - KhÃ´ng chuyá»ƒn tiáº¿p email nÃ y cho ngÆ°á»i khÃ¡c.
            - Náº¿u nghi ngá» tÃ i khoáº£n bá»‹ lá»™, liÃªn há»‡ admin/bá»™ pháº­n phá»¥ trÃ¡ch Ä‘á»ƒ Ä‘á»•i hoáº·c khÃ³a tÃ i khoáº£n.
            - ÄÄƒng xuáº¥t khá»i thiáº¿t bá»‹ dÃ¹ng chung sau khi sá»­ dá»¥ng.

            TrÃ¢n trá»ng,
            CPMS
            """;
    }

    private static string BuildPasswordResetPlainText(UserAccount account, string resetUrl, DateTimeOffset expiresAt)
    {
        var expiryText = expiresAt.ToLocalTime().ToString("HH:mm dd/MM/yyyy");
        return $"""
            Äáº·t láº¡i máº­t kháº©u Course Assistant

            Xin chÃ o {account.FullName},

            Há»‡ thá»‘ng nháº­n Ä‘Æ°á»£c yÃªu cáº§u Ä‘áº·t láº¡i máº­t kháº©u cho tÃ i khoáº£n {account.Email}.

            Link Ä‘áº·t láº¡i máº­t kháº©u:
            {resetUrl}

            Link nÃ y háº¿t háº¡n lÃºc {expiryText}. Náº¿u báº¡n khÃ´ng yÃªu cáº§u thao tÃ¡c nÃ y, hÃ£y bá» qua email.

            TrÃ¢n trá»ng,
            CPMS
            """;
    }

    private static string BuildPasswordResetHtml(UserAccount account, string resetUrl, DateTimeOffset expiresAt)
    {
        var fullName = WebUtility.HtmlEncode(account.FullName);
        var email = WebUtility.HtmlEncode(account.Email);
        var resetLink = WebUtility.HtmlEncode(resetUrl);
        var expiryText = WebUtility.HtmlEncode(expiresAt.ToLocalTime().ToString("HH:mm dd/MM/yyyy"));

        return $$"""
            <!doctype html>
            <html lang="vi">
            <body style="margin:0;background:#f4f7fb;font-family:Arial,Helvetica,sans-serif;color:#102033;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f4f7fb;padding:28px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:620px;background:#ffffff;border:1px solid #dbe4ef;border-radius:12px;overflow:hidden;">
                      <tr>
                        <td style="padding:28px;">
                          <h1 style="margin:0 0 14px;color:#0f4c81;font-size:24px;line-height:1.3;">Äáº·t láº¡i máº­t kháº©u</h1>
                          <p style="font-size:16px;line-height:1.6;margin:0 0 16px;">Xin chÃ o <strong>{{fullName}}</strong>,</p>
                          <p style="font-size:15px;line-height:1.6;margin:0 0 18px;">Há»‡ thá»‘ng nháº­n Ä‘Æ°á»£c yÃªu cáº§u Ä‘áº·t láº¡i máº­t kháº©u cho tÃ i khoáº£n <strong>{{email}}</strong>.</p>
                          <p style="margin:22px 0;">
                            <a href="{{resetLink}}" style="display:inline-block;background:#0f4c81;color:#ffffff;text-decoration:none;padding:12px 18px;border-radius:8px;font-weight:700;">Äáº·t láº¡i máº­t kháº©u</a>
                          </p>
                          <p style="font-size:14px;color:#64748b;line-height:1.6;margin:0 0 12px;">Link háº¿t háº¡n lÃºc <strong>{{expiryText}}</strong>.</p>
                          <p style="font-size:13px;color:#64748b;line-height:1.6;margin:0;">Náº¿u báº¡n khÃ´ng yÃªu cáº§u thao tÃ¡c nÃ y, hÃ£y bá» qua email.</p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string BuildHtml(
        UserAccount account,
        string temporaryPassword,
        string? loginUrl,
        IReadOnlyList<string>? subjectLabels)
    {
        var fullName = WebUtility.HtmlEncode(account.FullName);
        var email = WebUtility.HtmlEncode(account.Email);
        var role = WebUtility.HtmlEncode(account.Role);
        var accessSummary = WebUtility.HtmlEncode(BuildAccessSummary(account, subjectLabels));
        var password = WebUtility.HtmlEncode(temporaryPassword);
        var loginLink = string.IsNullOrWhiteSpace(loginUrl)
            ? "<p style=\"margin:0;color:#475569;\">ÄÄƒng nháº­p táº¡i Ä‘á»‹a chá»‰ á»©ng dá»¥ng Ä‘Æ°á»£c nhÃ  trÆ°á»ng cung cáº¥p.</p>"
            : $"""
              <a href="{WebUtility.HtmlEncode(loginUrl)}" style="display:inline-block;background:#0f4c81;color:#ffffff;text-decoration:none;padding:12px 18px;border-radius:8px;font-weight:700;">ÄÄƒng nháº­p á»©ng dá»¥ng</a>
              """;

        return $$"""
            <!doctype html>
            <html lang="vi">
            <body style="margin:0;background:#f4f7fb;font-family:Arial,Helvetica,sans-serif;color:#102033;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f4f7fb;padding:28px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:680px;background:#ffffff;border:1px solid #dbe4ef;border-radius:12px;overflow:hidden;">
                      <tr>
                        <td style="padding:24px 28px 12px;text-align:center;">
                          <img src="cid:{{LogoContentId}}" alt="FPT Education" style="max-width:360px;width:80%;height:auto;margin:0 auto 18px;display:block;" />
                          <h1 style="margin:0;color:#0f4c81;font-size:24px;line-height:1.3;">ChÃ o má»«ng báº¡n tá»›i á»¨ng dá»¥ng Chat Bot - Quáº£n LÃ½ TÃ i Liá»‡u</h1>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:10px 28px 28px;">
                          <p style="font-size:16px;line-height:1.6;margin:0 0 16px;">Xin chÃ o <strong>{{fullName}}</strong>,</p>
                          <p style="font-size:15px;line-height:1.6;margin:0 0 18px;">TÃ i khoáº£n cá»§a báº¡n Ä‘Ã£ Ä‘Æ°á»£c táº¡o thÃ nh cÃ´ng. Vui lÃ²ng sá»­ dá»¥ng thÃ´ng tin bÃªn dÆ°á»›i Ä‘á»ƒ Ä‘Äƒng nháº­p há»‡ thá»‘ng.</p>

                          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="border-collapse:collapse;margin:18px 0;background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;overflow:hidden;">
                            <tr>
                              <td style="padding:12px 14px;color:#64748b;width:38%;font-size:14px;">Email Ä‘Äƒng nháº­p</td>
                              <td style="padding:12px 14px;font-weight:700;font-size:14px;">{{email}}</td>
                            </tr>
                            <tr>
                              <td style="padding:12px 14px;color:#64748b;font-size:14px;">Vai trÃ²</td>
                              <td style="padding:12px 14px;font-weight:700;font-size:14px;">{{role}}</td>
                            </tr>
                            <tr>
                              <td style="padding:12px 14px;color:#64748b;font-size:14px;">MÃ´n / quyá»n truy cáº­p</td>
                              <td style="padding:12px 14px;font-weight:700;font-size:14px;">{{accessSummary}}</td>
                            </tr>
                            <tr>
                              <td style="padding:12px 14px;color:#64748b;font-size:14px;">Máº­t kháº©u táº¡m thá»i</td>
                              <td style="padding:12px 14px;font-weight:800;font-size:14px;color:#b42318;">{{password}}</td>
                            </tr>
                          </table>

                          <div style="margin:20px 0;">{{loginLink}}</div>

                          <div style="border-left:4px solid #f97316;background:#fff7ed;padding:14px 16px;border-radius:8px;margin-top:22px;">
                            <h2 style="margin:0 0 8px;font-size:16px;color:#9a3412;">Nháº¯c nhá»Ÿ báº£o máº­t tÃ i khoáº£n</h2>
                            <ul style="margin:0;padding-left:18px;color:#7c2d12;font-size:14px;line-height:1.6;">
                              <li>KhÃ´ng chia sáº» máº­t kháº©u cho báº¥t ká»³ ai.</li>
                              <li>KhÃ´ng chuyá»ƒn tiáº¿p email nÃ y cho ngÆ°á»i khÃ¡c.</li>
                              <li>Náº¿u nghi ngá» tÃ i khoáº£n bá»‹ lá»™, liÃªn há»‡ admin/bá»™ pháº­n phá»¥ trÃ¡ch Ä‘á»ƒ Ä‘á»•i hoáº·c khÃ³a tÃ i khoáº£n.</li>
                              <li>ÄÄƒng xuáº¥t khá»i thiáº¿t bá»‹ dÃ¹ng chung sau khi sá»­ dá»¥ng.</li>
                            </ul>
                          </div>

                          <p style="margin:22px 0 0;color:#64748b;font-size:13px;line-height:1.5;">Email nÃ y Ä‘Æ°á»£c gá»­i tá»« há»‡ thá»‘ng CPMS. Vui lÃ²ng khÃ´ng tráº£ lá»i trá»±c tiáº¿p email nÃ y.</p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string BuildAccessSummary(UserAccount account, IReadOnlyList<string>? subjectLabels)
    {
        if (account.Role == AppRoles.Admin)
        {
            return "Admin - toÃ n quyá»n há»‡ thá»‘ng";
        }

        if (account.Role == AppRoles.Lecturer)
        {
            var subjects = subjectLabels?
                .Where(subject => !string.IsNullOrWhiteSpace(subject))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            return subjects.Count == 0
                ? "Lecturer - chÆ°a Ä‘Æ°á»£c gÃ¡n mÃ´n"
                : string.Join(", ", subjects);
        }

        return "Student - táº¥t cáº£ tÃ i liá»‡u Ä‘Ã£ index";
    }
}

