using Microsoft.Extensions.Options;
using NSubstitute;
using PRN222_FINAL.BLL.Contracts.Billing;
using PRN222_FINAL.BLL.Options;
using PRN222_FINAL.BLL.Services.Billing;
using PRN222_FINAL.BLL.Services.Billing.Gateways;
using PRN222_FINAL.DAL.Entities.Billing;
using PRN222_FINAL.DAL.Enums;
using PRN222_FINAL.DAL.Repositories.Billing;
using Xunit;

namespace PRN222_FINAL.BLL.Tests;

public sealed class PaymentServiceTests
{
    [Fact]
    public async Task CreateCheckoutAsync_RejectsDowngradeWhileCurrentPackageIsActive()
    {
        var userId = Guid.NewGuid();
        var lowerPackage = CreatePackage("STUDENT", "Student", 20);
        var currentPackage = CreatePackage("PRO", "Pro", 30);
        var packages = Substitute.For<IPackageRepository>();
        var payments = Substitute.For<IPaymentRepository>();
        var subscriptions = Substitute.For<ISubscriptionRepository>();
        packages.GetByIdAsync(lowerPackage.Id, Arg.Any<CancellationToken>()).Returns(lowerPackage);
        subscriptions.GetCurrentActiveAsync(userId, Arg.Any<CancellationToken>()).Returns(new KnowledgeSqlSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PackageId = currentPackage.Id,
            Package = currentPackage,
            Status = SubscriptionStatus.Active,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-1),
            EndsAt = DateTimeOffset.UtcNow.AddDays(20)
        });
        var service = new PaymentService(
            packages,
            payments,
            subscriptions,
            Substitute.For<IMomoPaymentGateway>(),
            Substitute.For<IPayOsPaymentGateway>(),
            Microsoft.Extensions.Options.Options.Create(new PaymentOptions()));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateCheckoutAsync(new CreatePaymentRequestDto
        {
            UserId = userId,
            PackageId = lowerPackage.Id,
            Provider = PRN222_FINAL.BLL.Models.PaymentProvider.MoMo,
            ReturnUrl = "https://example.test/return",
            CancelUrl = "https://example.test/cancel"
        }));

        Assert.Contains("Không thể hạ", exception.Message);
        await payments.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    private static KnowledgeSqlPackage CreatePackage(string code, string name, int sortOrder) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Name = name,
        IsActive = true,
        PriceVnd = 49_000,
        DurationDays = 30,
        SortOrder = sortOrder
    };
}
