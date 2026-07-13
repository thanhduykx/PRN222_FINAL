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
    public async Task CreateCheckoutAsync_FreePackage_UsesAtomicClaimAndRejectsSecondActivation()
    {
        var userId = Guid.NewGuid();
        var freePackage = CreatePackage("FREE", "Free", 10);
        freePackage.PriceVnd = 0;
        var packages = Substitute.For<IPackageRepository>();
        var payments = Substitute.For<IPaymentRepository>();
        var subscriptions = Substitute.For<ISubscriptionRepository>();
        packages.GetByIdAsync(freePackage.Id, Arg.Any<CancellationToken>()).Returns(freePackage);
        payments.TryAddFreePaidAsync(Arg.Any<KnowledgeSqlPayment>(), Arg.Any<CancellationToken>()).Returns(false);
        var service = CreateService(packages, payments, subscriptions);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateCheckoutAsync(new CreatePaymentRequestDto
        {
            UserId = userId,
            PackageId = freePackage.Id,
            Provider = PRN222_FINAL.BLL.Models.PaymentProvider.MoMo,
            ReturnUrl = "https://app.example.test/Payments/Return",
            CancelUrl = "https://app.example.test/Packages"
        }));

        Assert.Contains("một lần", error.Message);
        await payments.Received(1).TryAddFreePaidAsync(Arg.Any<KnowledgeSqlPayment>(), Arg.Any<CancellationToken>());
        await payments.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task CreateCheckoutAsync_BuildsWebhookFromCurrentPublicReturnOrigin()
    {
        var package = CreatePackage("PRO", "Pro", 30);
        var packages = Substitute.For<IPackageRepository>();
        var payments = Substitute.For<IPaymentRepository>();
        var subscriptions = Substitute.For<ISubscriptionRepository>();
        var momo = Substitute.For<IMomoPaymentGateway>();
        PaymentGatewayCreateRequest? captured = null;
        packages.GetByIdAsync(package.Id, Arg.Any<CancellationToken>()).Returns(package);
        momo.CreateCheckoutAsync(Arg.Any<PaymentGatewayCreateRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                captured = call.ArgAt<PaymentGatewayCreateRequest>(0);
                return new PaymentGatewayCreateResult { CheckoutUrl = "https://gateway.example/checkout" };
            });
        var service = new PaymentService(
            packages, payments, subscriptions, momo, Substitute.For<IPayOsPaymentGateway>(),
            Microsoft.Extensions.Options.Options.Create(new PaymentOptions()));

        await service.CreateCheckoutAsync(new CreatePaymentRequestDto
        {
            UserId = Guid.NewGuid(),
            PackageId = package.Id,
            Provider = PRN222_FINAL.BLL.Models.PaymentProvider.MoMo,
            ReturnUrl = "https://course.example.edu/Payments/Return",
            CancelUrl = "https://course.example.edu/Packages"
        });

        Assert.NotNull(captured);
        Assert.Equal("https://course.example.edu/Payments/MomoWebhook", captured!.IpnUrl);
    }

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

    private static PaymentService CreateService(
        IPackageRepository packages,
        IPaymentRepository payments,
        ISubscriptionRepository subscriptions) => new(
        packages,
        payments,
        subscriptions,
        Substitute.For<IMomoPaymentGateway>(),
        Substitute.For<IPayOsPaymentGateway>(),
        Microsoft.Extensions.Options.Options.Create(new PaymentOptions()));
}
