using PRN222_FINAL.DAL.Models.Email;

namespace PRN222_FINAL.DAL.Repositories.Email;

public interface IEmailRepository
{
    Task SendAsync(EmailMessageData message, CancellationToken cancellationToken = default);
}
