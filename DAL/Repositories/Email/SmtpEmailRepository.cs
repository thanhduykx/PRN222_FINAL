using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using PRN222_FINAL.DAL.Models.Email;

namespace PRN222_FINAL.DAL.Repositories.Email;

public sealed class SmtpEmailRepository : IEmailRepository
{
    private readonly SmtpSettingsData _settings;
    public SmtpEmailRepository(SmtpSettingsData settings) => _settings = settings;

    public async Task SendAsync(EmailMessageData data, CancellationToken cancellationToken = default)
    {
        ValidateSettings();
        using var message = new MailMessage
        {
            From = new MailAddress(_settings.FromEmail, _settings.FromName, Encoding.UTF8),
            Subject = data.Subject, SubjectEncoding = Encoding.UTF8, BodyEncoding = Encoding.UTF8
        };
        message.To.Add(new MailAddress(data.RecipientEmail, data.RecipientName, Encoding.UTF8));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(data.PlainTextBody, Encoding.UTF8, MediaTypeNames.Text.Plain));
        var html = AlternateView.CreateAlternateViewFromString(data.HtmlBody, Encoding.UTF8, MediaTypeNames.Text.Html);
        if (!string.IsNullOrWhiteSpace(data.InlineLogoPath) && File.Exists(data.InlineLogoPath))
        {
            var logo = new LinkedResource(data.InlineLogoPath, "image/png")
            { ContentId = data.InlineLogoContentId ?? "inline-logo", TransferEncoding = TransferEncoding.Base64 };
            logo.ContentType.Name = Path.GetFileName(data.InlineLogoPath);
            html.LinkedResources.Add(logo);
        }
        message.AlternateViews.Add(html);
        var password = NormalizePassword(_settings.Host, _settings.Password);
        using var client = new SmtpClient(_settings.Host, _settings.Port)
        { EnableSsl=_settings.EnableSsl,Credentials=new NetworkCredential(_settings.UserName,password),DeliveryMethod=SmtpDeliveryMethod.Network };
        await client.SendMailAsync(message, cancellationToken);
    }

    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(_settings.Host) || string.IsNullOrWhiteSpace(_settings.FromEmail)
            || string.IsNullOrWhiteSpace(_settings.UserName) || string.IsNullOrWhiteSpace(_settings.Password))
            throw new InvalidOperationException("SMTP configuration is incomplete.");
    }

    private static string NormalizePassword(string host, string password)
    {
        if (!host.Equals("smtp.gmail.com", StringComparison.OrdinalIgnoreCase)) return password;
        var compact = string.Concat(password.Where(c => !char.IsWhiteSpace(c)));
        return compact.Length == 16 && compact.All(char.IsLetterOrDigit) ? compact : password;
    }
}
