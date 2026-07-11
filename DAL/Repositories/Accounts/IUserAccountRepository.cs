using PRN222_FINAL.DAL.Models.Accounts;

namespace PRN222_FINAL.DAL.Repositories.Accounts;

public interface IUserAccountRepository
{
    Task<List<UserAccountData>> LoadAllAsync(CancellationToken cancellationToken = default);
    Task SaveAllAsync(IReadOnlyCollection<UserAccountData> users, CancellationToken cancellationToken = default);
}
