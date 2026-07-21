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
    public async Task CreateCheckoutAsync_PaidPackage_ExpiresAfterTenMinutes()
    {
        var now = new DateTimeOffset(2026, 7, 16, 8, 0, 0, TimeSpan.Zero);
        var package = CreatePackage("PRO", "Pro", 30);
        var packages = Substitute.For<IPackageRepository>();
        var payments = Substitute.For<IPaymentRepository>();
        var subscriptions = Substitute.For<ISubscriptionRepository>();
        var momo = Substitute.For<IMomoPaymentGateway>();
        packages.GetByIdAsync(package.Id, Arg.Any<CancellationToken>()).Returns(package);
        momo.CreateCheckoutAsync(Arg.Any<PaymentGatewayCreateRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PaymentGatewayCreateResult { CheckoutUrl = "https://gateway.example/checkout" });
        var service = new PaymentService(
            packages, payments, subscriptions, momo, Substitute.For<IPayOsPaymentGateway>(),
            Microsoft.Extensions.Options.Options.Create(new PaymentOptions()), new FixedTimeProvider(now));

        var result = await service.CreateCheckoutAsync(new CreatePaymentRequestDto
        {
            UserId = Guid.NewGuid(),
            PackageId = package.Id,
            Provider = PRN222_FINAL.BLL.Models.PaymentProvider.MoMo,
            ReturnUrl = "https://app.example.test/Payments/Return",
            CancelUrl = "https://app.example.test/Payments/Checkout"
        });

        Assert.Equal(now.AddMinutes(10), result.ExpiresAt);
        await payments.Received(1).DeleteExpiredPendingAsync(now.AddMinutes(-10), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPendingPaymentsAsync_DeletesExpiredOrdersBeforeReturningCart()
    {
        var now = new DateTimeOffset(2026, 7, 16, 8, 0, 0, TimeSpan.Zero);
        var userId = Guid.NewGuid();
        var package = CreatePackage("PRO", "Pro", 30);
        var pending = new KnowledgeSqlPayment
        {
            Id = Guid.NewGuid(), UserId = userId, PackageId = package.Id, Package = package,
            UserName = "Nguyen Van A", UserEmail = "student@example.edu",
            Provider = PaymentProvider.PayOS, Status = PaymentStatus.Pending,
            AmountVnd = package.PriceVnd, OrderCode = "123456789", CheckoutUrl = "https://pay.example/123",
            QrCode = "00020101021238570010A000000727",
            RawResponse = """{"code":"00","data":{"accountName":"COURSE ASSISTANT","accountNumber":"1234567890","bin":"970422","description":"PAY 123456789"}}""",
            CreatedAt = now.AddMinutes(-4)
        };
        var payments = Substitute.For<IPaymentRepository>();
        payments.GetPendingByUserAsync(userId, Arg.Any<CancellationToken>()).Returns([pending]);
        var service = CreateService(
            Substitute.For<IPackageRepository>(), payments, Substitute.For<ISubscriptionRepository>(),
            new FixedTimeProvider(now));

        var result = await service.GetPendingPaymentsAsync(userId);

        await payments.Received(1).DeleteExpiredPendingAsync(now.AddMinutes(-10), Arg.Any<CancellationToken>());
        var item = Assert.Single(result);
        Assert.Equal(pending.Id, item.PaymentId);
        Assert.Equal(now.AddMinutes(6), item.ExpiresAt);
        Assert.Equal(pending.UserName, item.RecipientName);
        Assert.Equal(pending.UserEmail, item.RecipientEmail);
        Assert.Equal("COURSE ASSISTANT", item.PayeeAccountName);
        Assert.Equal("1234567890", item.PayeeAccountNumber);
        Assert.Equal("970422", item.PayeeBankBin);
        Assert.Equal("PAY 123456789", item.TransferDescription);
    }

    [Fact]
    public async Task DeletePendingPaymentAsync_DeletesOnlyOwnersPendingOrder()
    {
        var userId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var payments = Substitute.For<IPaymentRepository>();
        payments.DeletePendingAsync(paymentId, userId, Arg.Any<CancellationToken>()).Returns(true);
        var service = CreateService(
            Substitute.For<IPackageRepository>(), payments, Substitute.For<ISubscriptionRepository>());

        var deleted = await service.DeletePendingPaymentAsync(paymentId, userId);

        Assert.True(deleted);
        await payments.Received(1).DeletePendingAsync(paymentId, userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWebhookAsync_ExpiredPendingOrder_IsDeletedAndCannotActivateSubscription()
    {
        var now = new DateTimeOffset(2026, 7, 16, 8, 0, 0, TimeSpan.Zero);
        var payment = new KnowledgeSqlPayment
        {
            Id = Guid.NewGuid(), UserId = Guid.NewGuid(), PackageId = Guid.NewGuid(),
            Provider = PaymentProvider.MoMo, Status = PaymentStatus.Pending,
            AmountVnd = 49_000, OrderCode = "8123456789", CreatedAt = now.AddMinutes(-11)
        };
        var payments = Substitute.For<IPaymentRepository>();
        var subscriptions = Substitute.For<ISubscriptionRepository>();
        var momo = Substitute.For<IMomoPaymentGateway>();
        payments.GetByOrderCodeAsync(PaymentProvider.MoMo, payment.OrderCode, Arg.Any<CancellationToken>())
            .Returns(payment);
        payments.DeletePendingAsync(payment.Id, payment.UserId, Arg.Any<CancellationToken>()).Returns(true);
        momo.VerifyWebhook(Arg.Any<PaymentWebhookDto>()).Returns(new PaymentGatewayWebhookResult
        {
            IsSignatureValid = true,
            OrderCode = payment.OrderCode,
            AmountVnd = payment.AmountVnd,
            Status = PRN222_FINAL.BLL.Models.PaymentStatus.Paid
        });
        var service = new PaymentService(
            Substitute.For<IPackageRepository>(), payments, subscriptions, momo,
            Substitute.For<IPayOsPaymentGateway>(),
            Microsoft.Extensions.Options.Options.Create(new PaymentOptions()), new FixedTimeProvider(now));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.HandleWebhookAsync(new PaymentWebhookDto
        {
            Provider = PRN222_FINAL.BLL.Models.PaymentProvider.MoMo,
            RawBody = "{}"
        }));

        Assert.Contains("hết hạn", error.Message);
        await payments.Received(1).DeletePendingAsync(payment.Id, payment.UserId, Arg.Any<CancellationToken>());
        await subscriptions.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

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

    [Fact]
    public async Task GetReturnStatusAsync_ReturnsReceiptAndActivatedPackageForOwner()
    {
        var userId = Guid.NewGuid();
        var package = CreatePackage("PRO", "Pro", 30);
        package.MonthlyChatLimit = 500;
        package.MonthlyDocumentUploadLimit = 20;
        package.StorageLimitMb = 1_024;
        var paidAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        var payment = new KnowledgeSqlPayment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserName = "Nguyen Van A",
            UserEmail = "student@example.edu",
            PackageId = package.Id,
            Provider = PaymentProvider.MoMo,
            Status = PaymentStatus.Paid,
            AmountVnd = package.PriceVnd,
            Currency = "VND",
            OrderCode = "8123456789",
            ProviderTransactionId = "MOMO-123",
            CreatedAt = paidAt.AddMinutes(-1),
            PaidAt = paidAt
        };
        var subscription = new KnowledgeSqlSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PackageId = package.Id,
            PaymentId = payment.Id,
            Status = SubscriptionStatus.Active,
            StartsAt = paidAt,
            EndsAt = paidAt.AddDays(30),
            CreatedAt = paidAt,
            Package = package
        };
        var packages = Substitute.For<IPackageRepository>();
        var payments = Substitute.For<IPaymentRepository>();
        var subscriptions = Substitute.For<ISubscriptionRepository>();
        payments.GetByOrderCodeAsync(PaymentProvider.MoMo, payment.OrderCode, Arg.Any<CancellationToken>()).Returns(payment);
        payments.GetLatestSuccessfulByUserAsync(userId, Arg.Any<CancellationToken>()).Returns(payment);
        packages.GetByIdAsync(package.Id, Arg.Any<CancellationToken>()).Returns(package);
        subscriptions.GetByPaymentIdAsync(payment.Id, Arg.Any<CancellationToken>()).Returns(subscription);
        var service = CreateService(packages, payments, subscriptions);

        var result = await service.GetReturnStatusAsync(
            PRN222_FINAL.BLL.Models.PaymentProvider.MoMo,
            payment.OrderCode,
            userId);

        Assert.NotNull(result);
        Assert.Equal("Pro", result!.PackageName);
        Assert.Equal(49_000, result.AmountVnd);
        Assert.Equal("Nguyen Van A", result.CustomerName);
        Assert.Equal(subscription.EndsAt, result.SubscriptionEndsAt);
        Assert.Equal(20, result.MonthlyDocumentUploadLimit);
    }

    [Fact]
    public async Task GetReturnStatusAsync_PendingMomoPayment_ReconcilesAndActivatesSubscription()
    {
        var now = new DateTimeOffset(2026, 7, 21, 8, 0, 0, TimeSpan.Zero);
        var userId = Guid.NewGuid();
        var package = CreatePackage("PRO", "Pro", 30);
        var payment = new KnowledgeSqlPayment
        {
            Id = Guid.NewGuid(), UserId = userId, PackageId = package.Id,
            UserName = "Nguyen Van A", UserEmail = "student@example.edu",
            Provider = PaymentProvider.MoMo, Status = PaymentStatus.Pending,
            AmountVnd = package.PriceVnd, Currency = "VND", OrderCode = "8123456789",
            CreatedAt = now.AddMinutes(-1)
        };
        var packages = Substitute.For<IPackageRepository>();
        var payments = Substitute.For<IPaymentRepository>();
        var subscriptions = Substitute.For<ISubscriptionRepository>();
        var momo = Substitute.For<IMomoPaymentGateway>();
        payments.GetByOrderCodeAsync(PaymentProvider.MoMo, payment.OrderCode, Arg.Any<CancellationToken>()).Returns(payment);
        packages.GetByIdAsync(package.Id, Arg.Any<CancellationToken>()).Returns(package);
        momo.GetStatusAsync(payment.OrderCode, Arg.Any<CancellationToken>()).Returns(new PaymentGatewayStatusResult
        {
            OrderCode = payment.OrderCode,
            ProviderTransactionId = "MOMO-PAID-123",
            AmountVnd = payment.AmountVnd,
            Status = PRN222_FINAL.BLL.Models.PaymentStatus.Paid,
            Message = "Successful."
        });
        var service = new PaymentService(
            packages, payments, subscriptions, momo, Substitute.For<IPayOsPaymentGateway>(),
            Microsoft.Extensions.Options.Options.Create(new PaymentOptions()), new FixedTimeProvider(now));

        var result = await service.GetReturnStatusAsync(
            PRN222_FINAL.BLL.Models.PaymentProvider.MoMo,
            payment.OrderCode,
            userId);

        Assert.NotNull(result);
        Assert.Equal(PRN222_FINAL.BLL.Models.PaymentStatus.Paid, result!.Status);
        Assert.Equal("MOMO-PAID-123", result.ProviderTransactionId);
        await payments.Received().UpdateAsync(
            Arg.Is<KnowledgeSqlPayment>(item => item.Id == payment.Id && item.Status == PaymentStatus.Paid),
            Arg.Any<CancellationToken>());
        await subscriptions.Received().ActivateExclusiveAsync(
            Arg.Is<KnowledgeSqlSubscription>(item => item.PaymentId == payment.Id && item.Status == SubscriptionStatus.Active),
            now,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetReturnStatusAsync_MomoQueryAmountMismatch_DoesNotActivateSubscription()
    {
        var userId = Guid.NewGuid();
        var package = CreatePackage("PRO", "Pro", 30);
        var payment = new KnowledgeSqlPayment
        {
            Id = Guid.NewGuid(), UserId = userId, PackageId = package.Id,
            Provider = PaymentProvider.MoMo, Status = PaymentStatus.Pending,
            AmountVnd = package.PriceVnd, Currency = "VND", OrderCode = "8123456790",
            CreatedAt = DateTimeOffset.UtcNow
        };
        var packages = Substitute.For<IPackageRepository>();
        var payments = Substitute.For<IPaymentRepository>();
        var subscriptions = Substitute.For<ISubscriptionRepository>();
        var momo = Substitute.For<IMomoPaymentGateway>();
        payments.GetByOrderCodeAsync(PaymentProvider.MoMo, payment.OrderCode, Arg.Any<CancellationToken>()).Returns(payment);
        packages.GetByIdAsync(package.Id, Arg.Any<CancellationToken>()).Returns(package);
        momo.GetStatusAsync(payment.OrderCode, Arg.Any<CancellationToken>()).Returns(new PaymentGatewayStatusResult
        {
            OrderCode = payment.OrderCode,
            AmountVnd = payment.AmountVnd + 1,
            Status = PRN222_FINAL.BLL.Models.PaymentStatus.Paid
        });
        var service = new PaymentService(
            packages, payments, subscriptions, momo, Substitute.For<IPayOsPaymentGateway>(),
            Microsoft.Extensions.Options.Options.Create(new PaymentOptions()));

        var result = await service.GetReturnStatusAsync(
            PRN222_FINAL.BLL.Models.PaymentProvider.MoMo,
            payment.OrderCode,
            userId);

        Assert.NotNull(result);
        Assert.Equal(PRN222_FINAL.BLL.Models.PaymentStatus.Pending, result!.Status);
        await payments.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
        await subscriptions.DidNotReceiveWithAnyArgs().ActivateExclusiveAsync(default!, default, default);
    }

    [Fact]
    public async Task GetReturnStatusAsync_PendingPayOsPayment_ReconcilesAndActivatesSubscription()
    {
        var now = new DateTimeOffset(2026, 7, 21, 8, 0, 0, TimeSpan.Zero);
        var userId = Guid.NewGuid();
        var package = CreatePackage("PRO", "Pro", 30);
        var payment = new KnowledgeSqlPayment
        {
            Id = Guid.NewGuid(), UserId = userId, PackageId = package.Id,
            UserName = "Nguyen Van A", UserEmail = "student@example.edu",
            Provider = PaymentProvider.PayOS, Status = PaymentStatus.Pending,
            AmountVnd = package.PriceVnd, Currency = "VND", OrderCode = "260721123456789",
            CreatedAt = now.AddMinutes(-1)
        };
        var packages = Substitute.For<IPackageRepository>();
        var payments = Substitute.For<IPaymentRepository>();
        var subscriptions = Substitute.For<ISubscriptionRepository>();
        var payOs = Substitute.For<IPayOsPaymentGateway>();
        payments.GetByOrderCodeAsync(PaymentProvider.PayOS, payment.OrderCode, Arg.Any<CancellationToken>()).Returns(payment);
        packages.GetByIdAsync(package.Id, Arg.Any<CancellationToken>()).Returns(package);
        payOs.GetStatusAsync(260721123456789, Arg.Any<CancellationToken>()).Returns(new PaymentGatewayStatusResult
        {
            OrderCode = payment.OrderCode,
            ProviderTransactionId = "PAYOS-LINK-123",
            AmountVnd = payment.AmountVnd,
            Status = PRN222_FINAL.BLL.Models.PaymentStatus.Paid,
            Message = "PAID"
        });
        var service = new PaymentService(
            packages, payments, subscriptions, Substitute.For<IMomoPaymentGateway>(), payOs,
            Microsoft.Extensions.Options.Options.Create(new PaymentOptions()), new FixedTimeProvider(now));

        var result = await service.GetReturnStatusAsync(
            PRN222_FINAL.BLL.Models.PaymentProvider.PayOS,
            payment.OrderCode,
            userId);

        Assert.NotNull(result);
        Assert.Equal(PRN222_FINAL.BLL.Models.PaymentStatus.Paid, result!.Status);
        Assert.Equal("PAYOS-LINK-123", result.ProviderTransactionId);
        await payments.Received().UpdateAsync(
            Arg.Is<KnowledgeSqlPayment>(item => item.Id == payment.Id && item.Status == PaymentStatus.Paid),
            Arg.Any<CancellationToken>());
        await subscriptions.Received().ActivateExclusiveAsync(
            Arg.Is<KnowledgeSqlSubscription>(item => item.PaymentId == payment.Id && item.Status == SubscriptionStatus.Active),
            now,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetReturnStatusAsync_PayOsQueryWithoutAmount_DoesNotActivateSubscription()
    {
        var userId = Guid.NewGuid();
        var package = CreatePackage("PRO", "Pro", 30);
        var payment = new KnowledgeSqlPayment
        {
            Id = Guid.NewGuid(), UserId = userId, PackageId = package.Id,
            Provider = PaymentProvider.PayOS, Status = PaymentStatus.Pending,
            AmountVnd = package.PriceVnd, Currency = "VND", OrderCode = "260721123456790",
            CreatedAt = DateTimeOffset.UtcNow
        };
        var packages = Substitute.For<IPackageRepository>();
        var payments = Substitute.For<IPaymentRepository>();
        var subscriptions = Substitute.For<ISubscriptionRepository>();
        var payOs = Substitute.For<IPayOsPaymentGateway>();
        payments.GetByOrderCodeAsync(PaymentProvider.PayOS, payment.OrderCode, Arg.Any<CancellationToken>()).Returns(payment);
        packages.GetByIdAsync(package.Id, Arg.Any<CancellationToken>()).Returns(package);
        payOs.GetStatusAsync(260721123456790, Arg.Any<CancellationToken>()).Returns(new PaymentGatewayStatusResult
        {
            OrderCode = payment.OrderCode,
            Status = PRN222_FINAL.BLL.Models.PaymentStatus.Paid
        });
        var service = new PaymentService(
            packages, payments, subscriptions, Substitute.For<IMomoPaymentGateway>(), payOs,
            Microsoft.Extensions.Options.Options.Create(new PaymentOptions()));

        var result = await service.GetReturnStatusAsync(
            PRN222_FINAL.BLL.Models.PaymentProvider.PayOS,
            payment.OrderCode,
            userId);

        Assert.NotNull(result);
        Assert.Equal(PRN222_FINAL.BLL.Models.PaymentStatus.Pending, result!.Status);
        await payments.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
        await subscriptions.DidNotReceiveWithAnyArgs().ActivateExclusiveAsync(default!, default, default);
    }

    [Fact]
    public async Task GetReturnStatusAsync_DoesNotExposeAnotherUsersPayment()
    {
        var ownerId = Guid.NewGuid();
        var payment = new KnowledgeSqlPayment
        {
            Id = Guid.NewGuid(),
            UserId = ownerId,
            PackageId = Guid.NewGuid(),
            Provider = PaymentProvider.PayOS,
            Status = PaymentStatus.Paid,
            OrderCode = "123456789"
        };
        var packages = Substitute.For<IPackageRepository>();
        var payments = Substitute.For<IPaymentRepository>();
        var subscriptions = Substitute.For<ISubscriptionRepository>();
        payments.GetByOrderCodeAsync(PaymentProvider.PayOS, payment.OrderCode, Arg.Any<CancellationToken>()).Returns(payment);
        var service = CreateService(packages, payments, subscriptions);

        var result = await service.GetReturnStatusAsync(
            PRN222_FINAL.BLL.Models.PaymentProvider.PayOS,
            payment.OrderCode,
            Guid.NewGuid());

        Assert.Null(result);
        await packages.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
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
        ISubscriptionRepository subscriptions,
        TimeProvider? timeProvider = null) => new(
        packages,
        payments,
        subscriptions,
        Substitute.For<IMomoPaymentGateway>(),
        Substitute.For<IPayOsPaymentGateway>(),
        Microsoft.Extensions.Options.Options.Create(new PaymentOptions()),
        timeProvider ?? TimeProvider.System);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
