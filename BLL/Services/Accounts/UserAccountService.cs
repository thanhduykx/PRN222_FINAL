using PRN222_FINAL.BLL.Security;
using PRN222_FINAL.BLL.Models;
using System.Security.Cryptography;
using System.Text;

namespace PRN222_FINAL.BLL.Services.Accounts;

public interface IUserAccountService
{
    Task<IReadOnlyList<UserAccount>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserAccount>> GetByRoleAsync(string role, CancellationToken cancellationToken = default);
    Task<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default);
    Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<UserAccount> CreateLocalAsync(string fullName, string email, string password, CancellationToken cancellationToken = default);
    Task<UserAccount> CreateLocalForAdminAsync(string fullName, string email, string password, string role, CancellationToken cancellationToken = default);
    Task<UserAccount> GetOrCreateExternalAsync(string fullName, string email, string provider, CancellationToken cancellationToken = default);
    Task<UserAccount> UpdateFullNameAsync(Guid userId, string fullName, CancellationToken cancellationToken = default);
    Task<UserAccount> UpdateRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default);
    Task MarkActiveAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RecordLoginFailureAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RecordLoginSuccessAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserAccount> SetSuspendedAsync(Guid userId, bool isSuspended, CancellationToken cancellationToken = default);
    Task<UserAccount> DeleteAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<(UserAccount Account, string Token, DateTimeOffset ExpiresAt)?> CreatePasswordResetTokenAsync(
        string email,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default);
    Task<UserAccount> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default);
    Task<UserAccount> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);
    bool VerifyPassword(UserAccount account, string password);
}

public sealed record SeedAdminOptions(bool Enabled, string FullName, string Email, string Password);

public sealed class UserAccountService : IUserAccountService
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    private static readonly IReadOnlyList<SeedUserAccount> FixedAccounts =
    [
        new("Admin", "admin@gmail.com", "123456", AppRoles.Admin),
        new("Lecturer", "lecture@gmaail.com", "123456", AppRoles.Lecturer),
        new("Student", "student@gmail.com", "123456", AppRoles.Student)
    ];

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly PRN222_FINAL.DAL.Repositories.Accounts.IUserAccountRepository _repository;
    private readonly PRN222_FINAL.DAL.Repositories.IKnowledgeRepository _knowledgeRepository;
    private readonly SeedAdminOptions _seedAdmin;

    public UserAccountService(
        PRN222_FINAL.DAL.Repositories.Accounts.IUserAccountRepository repository,
        PRN222_FINAL.DAL.Repositories.IKnowledgeRepository knowledgeRepository,
        SeedAdminOptions? seedAdmin = null)
    {
        _repository = repository;
        _knowledgeRepository = knowledgeRepository;
        _seedAdmin = NormalizeSeedAdminOptions(seedAdmin);
    }

    public async Task<IReadOnlyList<UserAccount>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await LoadAsync(cancellationToken))
                .OrderBy(user => user.Email)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<UserAccount>> GetByRoleAsync(string role, CancellationToken cancellationToken = default)
    {
        var normalizedRole = AppRoles.Normalize(role);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await LoadAsync(cancellationToken))
                .Where(user => user.Role == normalizedRole)
                .OrderBy(user => user.FullName)
                .ThenBy(user => user.Email)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await LoadAsync(cancellationToken)).Count > 0;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadAsync(cancellationToken);
            return users.FirstOrDefault(user => string.Equals(user.Email, NormalizeEmail(email), StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadAsync(cancellationToken);
            return users.FirstOrDefault(user => user.Id == userId);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserAccount> CreateLocalAsync(string fullName, string email, string password, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadAsync(cancellationToken);
            var normalizedEmail = NormalizeEmail(email);
            if (users.Any(user => string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("This email is already registered.");
            }

            var user = new UserAccount
            {
                FullName = fullName.Trim(),
                Email = normalizedEmail,
                PasswordHash = HashPassword(password),
                Provider = "Local",
                Role = IsSeedAdminEmail(normalizedEmail) || users.Count == 0 ? AppRoles.Admin : AppRoles.Student
            };

            users.Add(user);
            await SaveAsync(user, cancellationToken);
            return user;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserAccount> CreateLocalForAdminAsync(
        string fullName,
        string email,
        string password,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (!AppRoles.IsKnown(role))
        {
            throw new InvalidOperationException("Role is invalid.");
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadAsync(cancellationToken);
            var normalizedEmail = NormalizeEmail(email);
            if (users.Any(user => string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("This email is already registered.");
            }

            var user = new UserAccount
            {
                FullName = NormalizeFullName(fullName),
                Email = normalizedEmail,
                PasswordHash = HashPassword(password),
                Provider = "Local",
                Role = IsSeedAdminEmail(normalizedEmail) ? AppRoles.Admin : AppRoles.Normalize(role)
            };

            users.Add(user);
            await SaveAsync(user, cancellationToken);
            return user;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserAccount> GetOrCreateExternalAsync(string fullName, string email, string provider, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadAsync(cancellationToken);
            var normalizedEmail = NormalizeEmail(email);
            var existing = users.FirstOrDefault(user => string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                var changed = EnsureSeedAdminRole(existing);
                if (TrackExternalProvider(existing, provider))
                {
                    changed = true;
                }

                if (ShouldUseExternalName(existing.FullName, fullName))
                {
                    existing.FullName = fullName.Trim();
                    changed = true;
                }

                if (changed)
                {
                    await SaveAsync(existing, cancellationToken);
                }

                return existing;
            }

            var user = new UserAccount
            {
                FullName = string.IsNullOrWhiteSpace(fullName) ? normalizedEmail : fullName.Trim(),
                Email = normalizedEmail,
                Provider = provider,
                Role = IsSeedAdminEmail(normalizedEmail) || users.Count == 0 ? AppRoles.Admin : AppRoles.Student
            };

            users.Add(user);
            await SaveAsync(user, cancellationToken);
            return user;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserAccount> UpdateRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!AppRoles.IsKnown(role))
        {
            throw new InvalidOperationException("Role is invalid.");
        }

        var normalizedRole = AppRoles.Normalize(role);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadAsync(cancellationToken);
            var user = users.FirstOrDefault(item => item.Id == userId)
                ?? throw new InvalidOperationException("User not found.");

            if (user.Role == AppRoles.Admin
                && normalizedRole != AppRoles.Admin
                && users.Count(item => item.Role == AppRoles.Admin) <= 1)
            {
                throw new InvalidOperationException("Cannot demote the last admin.");
            }

            if (IsSeedAdminEmail(user.Email) && normalizedRole != AppRoles.Admin)
            {
                throw new InvalidOperationException("Cannot demote the seed admin.");
            }

            user.Role = normalizedRole;
            await SaveAsync(user, cancellationToken);
            return user;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserAccount> UpdateFullNameAsync(Guid userId, string fullName, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadAsync(cancellationToken);
            var user = users.FirstOrDefault(item => item.Id == userId)
                ?? throw new InvalidOperationException("User not found.");

            user.FullName = NormalizeFullName(fullName);
            await SaveAsync(user, cancellationToken);
            return user;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserAccount> DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadAsync(cancellationToken);
            var user = users.FirstOrDefault(item => item.Id == userId)
                ?? throw new InvalidOperationException("User not found.");

            if (user.Role != AppRoles.Student && user.Role != AppRoles.Lecturer)
            {
                throw new InvalidOperationException("Set role to Student or Lecturer before deleting this user.");
            }

            if (user.Role == AppRoles.Admin && users.Count(item => item.Role == AppRoles.Admin) <= 1)
            {
                throw new InvalidOperationException("Cannot delete the last admin.");
            }

            if (IsSeedAdminEmail(user.Email))
            {
                throw new InvalidOperationException("Cannot delete the seed admin.");
            }

            var lastActivity = user.LastActiveAt ?? user.CreatedAt;
            if (lastActivity >= DateTimeOffset.UtcNow.AddMonths(-3))
            {
                throw new InvalidOperationException("User must be inactive for more than 3 months before deletion.");
            }

            var subjects = await _knowledgeRepository.GetCourseCatalogAsync(cancellationToken);
            if (subjects.Any(subject => subject.OwnerUserId == userId))
            {
                throw new InvalidOperationException("User cannot be deleted while responsible for a subject.");
            }

            foreach (var subject in subjects)
            {
                var lecturerIds = await _knowledgeRepository.GetSubjectLecturerIdsAsync(subject.Id, cancellationToken);
                if (lecturerIds.Contains(userId))
                {
                    throw new InvalidOperationException("User cannot be deleted while assigned to a subject.");
                }
            }

            await _repository.DeleteAsync(user.Id, cancellationToken);
            return user;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MarkActiveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new InvalidOperationException("User is required.");
        await _repository.UpdateLastActiveAsync(userId, DateTimeOffset.UtcNow, cancellationToken);
    }

    public async Task RecordLoginFailureAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new InvalidOperationException("User is required.");
        await _repository.RecordLoginFailureAsync(
            userId,
            maxFailures: 5,
            DateTimeOffset.UtcNow.AddMinutes(15),
            cancellationToken);
    }

    public async Task RecordLoginSuccessAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new InvalidOperationException("User is required.");
        await _repository.ResetLoginFailuresAsync(userId, cancellationToken);
        await _repository.UpdateLastActiveAsync(userId, DateTimeOffset.UtcNow, cancellationToken);
    }

    public async Task<UserAccount> SetSuspendedAsync(Guid userId, bool isSuspended, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadAsync(cancellationToken);
            var user = users.FirstOrDefault(item => item.Id == userId)
                ?? throw new InvalidOperationException("User not found.");
            if (isSuspended
                && user.Role == AppRoles.Admin
                && users.Count(item => item.Role == AppRoles.Admin && !item.IsSuspended) <= 1)
            {
                throw new InvalidOperationException("Cannot suspend the last active admin.");
            }

            var changedAt = DateTimeOffset.UtcNow;
            await _repository.SetSuspendedAsync(userId, isSuspended, changedAt, cancellationToken);
            user.IsSuspended = isSuspended;
            user.SuspendedAt = isSuspended ? changedAt : null;
            return user;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<(UserAccount Account, string Token, DateTimeOffset ExpiresAt)?> CreatePasswordResetTokenAsync(
        string email,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var expiresAt = DateTimeOffset.UtcNow.Add(lifetime <= TimeSpan.Zero ? TimeSpan.FromMinutes(30) : lifetime);
        var token = CreatePasswordResetToken();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadAsync(cancellationToken);
            var user = users.FirstOrDefault(item => string.Equals(item.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase));
            if (user is null)
            {
                return null;
            }

            user.PasswordResetTokenHash = HashResetToken(token);
            user.PasswordResetTokenExpiresAt = expiresAt;
            await SaveAsync(user, cancellationToken);
            return (user, token, expiresAt);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserAccount> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        ValidatePassword(newPassword);
        var normalizedEmail = NormalizeEmail(email);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadAsync(cancellationToken);
            var user = users.FirstOrDefault(item => string.Equals(item.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("Password reset link is invalid or expired.");

            if (string.IsNullOrWhiteSpace(user.PasswordResetTokenHash)
                || user.PasswordResetTokenExpiresAt is null
                || user.PasswordResetTokenExpiresAt <= DateTimeOffset.UtcNow
                || !ResetTokenMatches(user.PasswordResetTokenHash, token))
            {
                throw new InvalidOperationException("Password reset link is invalid or expired.");
            }

            user.PasswordHash = HashPassword(newPassword);
            user.PasswordResetTokenHash = null;
            user.PasswordResetTokenExpiresAt = null;
            user.PasswordChangedAt = DateTimeOffset.UtcNow;
            await SaveAsync(user, cancellationToken);
            return user;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserAccount> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        ValidatePassword(newPassword);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadAsync(cancellationToken);
            var user = users.FirstOrDefault(item => item.Id == userId)
                ?? throw new InvalidOperationException("User not found.");

            if (string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                throw new InvalidOperationException("This account does not have a local password. Use forgot password to set one.");
            }

            if (!VerifyPassword(user, currentPassword))
            {
                throw new InvalidOperationException("Current password is incorrect.");
            }

            user.PasswordHash = HashPassword(newPassword);
            user.PasswordResetTokenHash = null;
            user.PasswordResetTokenExpiresAt = null;
            user.PasswordChangedAt = DateTimeOffset.UtcNow;
            await SaveAsync(user, cancellationToken);
            return user;
        }
        finally
        {
            _gate.Release();
        }
    }

    public bool VerifyPassword(UserAccount account, string password)
    {
        return PasswordMatches(account.PasswordHash, password);
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        var key = pbkdf2.GetBytes(KeySize);
        return $"PBKDF2.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    private static string CreatePasswordResetToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string HashResetToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static bool ResetTokenMatches(string expectedHash, string token)
    {
        try
        {
            var expected = Convert.FromHexString(expectedHash);
            var actual = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return expected.Length == actual.Length
                   && CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw new InvalidOperationException("Password must be at least 8 characters.");
        }
    }

    private async Task<List<UserAccount>> LoadAsync(CancellationToken cancellationToken)
    {
        var users = await LoadUsersCoreAsync(cancellationToken);
        var original = users.ToDictionary(user => user.Id, ToData);

        foreach (var user in users)
        {
            var normalizedRole = AppRoles.Normalize(user.Role);
            if (user.Role != normalizedRole)
            {
                user.Role = normalizedRole;
            }

            if (string.IsNullOrWhiteSpace(user.FullName))
            {
                user.FullName = user.Email;
            }

            if (user.PasswordResetTokenExpiresAt is { } resetExpiresAt
                && resetExpiresAt <= DateTimeOffset.UtcNow
                && !string.IsNullOrWhiteSpace(user.PasswordResetTokenHash))
            {
                user.PasswordResetTokenHash = null;
                user.PasswordResetTokenExpiresAt = null;
            }
        }

        EnsureFixedAccounts(users);
        EnsureSeedAdmin(users);

        foreach (var user in users)
        {
            var current = ToData(user);
            if (!original.TryGetValue(user.Id, out var previous) || !PersistenceEquals(previous, current))
            {
                await _repository.UpsertAsync(current, cancellationToken);
            }
        }

        return users;
    }

    private Task SaveAsync(UserAccount user, CancellationToken cancellationToken) =>
        _repository.UpsertAsync(ToData(user), cancellationToken);

    private async Task<List<UserAccount>> LoadUsersCoreAsync(CancellationToken cancellationToken) =>
        (await _repository.LoadAllAsync(cancellationToken)).Select(ToModel).ToList();

    private static PRN222_FINAL.DAL.Models.Accounts.UserAccountData ToData(UserAccount user) => new()
    {
        Id=user.Id,Email=user.Email,FullName=user.FullName,PasswordHash=user.PasswordHash,
        PasswordResetTokenHash=user.PasswordResetTokenHash,PasswordResetTokenExpiresAt=user.PasswordResetTokenExpiresAt,
        PasswordChangedAt=user.PasswordChangedAt,Provider=user.Provider,Role=user.Role,CreatedAt=user.CreatedAt,LastActiveAt=user.LastActiveAt,
        IsSuspended=user.IsSuspended,SuspendedAt=user.SuspendedAt,FailedLoginCount=user.FailedLoginCount,LockoutEnd=user.LockoutEnd
    };

    private static UserAccount ToModel(PRN222_FINAL.DAL.Models.Accounts.UserAccountData user) => new()
    {
        Id=user.Id,Email=user.Email,FullName=user.FullName,PasswordHash=user.PasswordHash,
        PasswordResetTokenHash=user.PasswordResetTokenHash,PasswordResetTokenExpiresAt=user.PasswordResetTokenExpiresAt,
        PasswordChangedAt=user.PasswordChangedAt,Provider=user.Provider,Role=user.Role,CreatedAt=user.CreatedAt,LastActiveAt=user.LastActiveAt,
        IsSuspended=user.IsSuspended,SuspendedAt=user.SuspendedAt,FailedLoginCount=user.FailedLoginCount,LockoutEnd=user.LockoutEnd
    };

    private static bool PersistenceEquals(
        PRN222_FINAL.DAL.Models.Accounts.UserAccountData left,
        PRN222_FINAL.DAL.Models.Accounts.UserAccountData right) =>
        left.Email == right.Email
        && left.FullName == right.FullName
        && left.PasswordHash == right.PasswordHash
        && left.PasswordResetTokenHash == right.PasswordResetTokenHash
        && left.PasswordResetTokenExpiresAt == right.PasswordResetTokenExpiresAt
        && left.PasswordChangedAt == right.PasswordChangedAt
        && left.Provider == right.Provider
        && left.Role == right.Role
        && left.CreatedAt == right.CreatedAt
        && left.LastActiveAt == right.LastActiveAt
        && left.IsSuspended == right.IsSuspended
        && left.SuspendedAt == right.SuspendedAt
        && left.FailedLoginCount == right.FailedLoginCount
        && left.LockoutEnd == right.LockoutEnd;
    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static string NormalizeFullName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new InvalidOperationException("Full name is required.");
        }

        var normalized = fullName.Trim();
        return normalized.Length <= 120
            ? normalized
            : throw new InvalidOperationException("Full name must be 120 characters or fewer.");
    }

    private void EnsureSeedAdmin(List<UserAccount> users)
    {
        if (!_seedAdmin.Enabled)
        {
            return;
        }

        var seedEmail = _seedAdmin.Email;
        var existing = users.FirstOrDefault(user => string.Equals(user.Email, seedEmail, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            if (string.IsNullOrWhiteSpace(existing.FullName))
            {
                existing.FullName = _seedAdmin.FullName;
            }

            if (string.IsNullOrWhiteSpace(existing.Provider))
            {
                existing.Provider = "Local";
            }

            if (!PasswordMatches(existing.PasswordHash, _seedAdmin.Password))
            {
                existing.PasswordHash = HashPassword(_seedAdmin.Password);
                existing.PasswordResetTokenHash = null;
                existing.PasswordResetTokenExpiresAt = null;
                existing.PasswordChangedAt = DateTimeOffset.UtcNow;
            }

            if (existing.Role != AppRoles.Admin)
            {
                existing.Role = AppRoles.Admin;
            }

            return;
        }

        if (users.Any(user => user.Role == AppRoles.Admin))
        {
            return;
        }

        users.Add(new UserAccount
        {
            FullName = _seedAdmin.FullName,
            Email = seedEmail,
            PasswordHash = HashPassword(_seedAdmin.Password),
            Provider = "Local",
            Role = AppRoles.Admin
        });

    }

    private static void EnsureFixedAccounts(List<UserAccount> users)
    {
        foreach (var fixedAccount in FixedAccounts)
        {
            var existing = users.FirstOrDefault(user =>
                string.Equals(user.Email, fixedAccount.Email, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                users.Add(new UserAccount
                {
                    FullName = fixedAccount.FullName,
                    Email = fixedAccount.Email,
                    PasswordHash = HashPassword(fixedAccount.Password),
                    Provider = "Local",
                    Role = fixedAccount.Role
                });
                continue;
            }

            if (existing.Provider != "Local")
            {
                existing.Provider = "Local";
            }

            if (existing.Role != fixedAccount.Role)
            {
                existing.Role = fixedAccount.Role;
            }

            // Fixed accounts are bootstrap identities only. Never overwrite user-edited
            // names or passwords after the account has been created.
        }

    }

    private bool IsSeedAdminEmail(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        return (_seedAdmin.Enabled
                && string.Equals(normalizedEmail, _seedAdmin.Email, StringComparison.OrdinalIgnoreCase))
               || FixedAccounts.Any(account =>
                   account.Role == AppRoles.Admin
                   && string.Equals(normalizedEmail, account.Email, StringComparison.OrdinalIgnoreCase));
    }

    private bool EnsureSeedAdminRole(UserAccount user)
    {
        if (!IsSeedAdminEmail(user.Email) || user.Role == AppRoles.Admin)
        {
            return false;
        }

        user.Role = AppRoles.Admin;
        return true;
    }

    private static bool TrackExternalProvider(UserAccount user, string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(user.Provider))
        {
            user.Provider = provider.Trim();
            return true;
        }

        var providers = user.Provider
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (providers.Any(item => item.Equals(provider, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        providers.Add(provider.Trim());
        user.Provider = string.Join(", ", providers);
        return true;
    }

    private static bool ShouldUseExternalName(string currentName, string externalName)
    {
        return !string.IsNullOrWhiteSpace(externalName)
               && (string.IsNullOrWhiteSpace(currentName)
                   || currentName.Equals("System Admin", StringComparison.OrdinalIgnoreCase)
                   || currentName.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                   || currentName.Contains('@'));
    }

    private static SeedAdminOptions NormalizeSeedAdminOptions(SeedAdminOptions? options)
    {
        var fullName = string.IsNullOrWhiteSpace(options?.FullName) ? "System Admin" : options.FullName.Trim();
        var email = string.IsNullOrWhiteSpace(options?.Email) ? "admin@eduvietrag.local" : NormalizeEmail(options.Email);
        var password = string.IsNullOrWhiteSpace(options?.Password) ? "Admin@12345" : options.Password;
        return new SeedAdminOptions(options?.Enabled ?? true, fullName, email, password);
    }

    private static bool PasswordMatches(string passwordHash, string password)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        var parts = passwordHash.Split('.', 3);
        if (parts.Length != 3 || parts[0] != "PBKDF2")
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[1]);
            var expected = Convert.FromBase64String(parts[2]);
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
            var actual = pbkdf2.GetBytes(KeySize);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private sealed record SeedUserAccount(string FullName, string Email, string Password, string Role);
}

