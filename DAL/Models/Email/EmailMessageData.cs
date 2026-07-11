namespace PRN222_FINAL.DAL.Models.Email;

public sealed record SmtpSettingsData(string Host, int Port, bool EnableSsl, string FromEmail,
    string FromName, string UserName, string Password);

public sealed record EmailMessageData(string RecipientEmail, string RecipientName, string Subject,
    string PlainTextBody, string HtmlBody, string? InlineLogoPath = null, string? InlineLogoContentId = null);
