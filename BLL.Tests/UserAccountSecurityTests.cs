using NSubstitute;
using PRN222_FINAL.BLL.Security;
using PRN222_FINAL.BLL.Services.Accounts;
using PRN222_FINAL.DAL.Models.Accounts;
using PRN222_FINAL.DAL.Repositories;
using PRN222_FINAL.DAL.Repositories.Accounts;
using Xunit;

namespace PRN222_FINAL.BLL.Tests;

public sealed class UserAccountSecurityTests
{
    [Fact]
    public async Task RecordLoginFailureAsync_UsesAtomicFiveAttemptLockout()
    {
        var repository = Substitute.For<IUserAccountRepository>();
        var service = CreateService(repository);
        var userId = Guid.NewGuid();

        await service.RecordLoginFailureAsync(userId);

        await repository.Received(1).RecordLoginFailureAsync(
            userId,
            5,
            Arg.Is<DateTimeOffset>(value => value > DateTimeOffset.UtcNow.AddMinutes(14)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetSuspendedAsync_RejectsLastActiveAdmin()
    {
        var repository = Substitute.For<IUserAccountRepository>();
        var adminId = Guid.NewGuid();
        repository.LoadAllAsync(Arg.Any<CancellationToken>()).Returns(new List<UserAccountData>
        {
            new()
            {
                Id = adminId,
                Email = "admin@gmail.com",
                FullName = "Admin",
                Provider = "Local",
                Role = AppRoles.Admin,
                CreatedAt = DateTimeOffset.UtcNow
            }
        });
        var service = CreateService(repository);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetSuspendedAsync(adminId, true));

        Assert.Contains("last active admin", error.Message, StringComparison.OrdinalIgnoreCase);
        await repository.DidNotReceiveWithAnyArgs().SetSuspendedAsync(default, default, default, default);
    }

    private static UserAccountService CreateService(IUserAccountRepository repository) => new(
        repository,
        Substitute.For<IKnowledgeRepository>(),
        new SeedAdminOptions(false, "Disabled", "disabled@example.test", "unused-password"));
}
