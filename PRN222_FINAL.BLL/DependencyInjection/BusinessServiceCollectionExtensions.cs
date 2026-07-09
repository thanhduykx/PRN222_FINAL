using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PRN222_FINAL.BLL.Options;
using PRN222_FINAL.BLL.Services.Billing;
using PRN222_FINAL.BLL.Services.Billing.Gateways;
using PRN222_FINAL.DAL.Repositories;
using PRN222_FINAL.DAL.Repositories.Billing;
using PRN222_FINAL.Models;

namespace PRN222_FINAL.BLL;

public static class BusinessServiceCollectionExtensions
{
    public static IServiceCollection AddKnowledgeBusinessServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;

        services.AddSingleton<IKnowledgeRepository>(_ => new SqlKnowledgeRepository(connectionString));
        services.AddSingleton<IKnowledgeService, KnowledgeService>();

        services.Configure<PaymentOptions>(configuration.GetSection("Payment"));
        services.AddSingleton<IPackageRepository>(_ => new PackageRepository(connectionString));
        services.AddSingleton<IPaymentRepository>(_ => new PaymentRepository(connectionString));
        services.AddSingleton<ISubscriptionRepository>(_ => new SubscriptionRepository(connectionString));
        services.AddScoped<IPackageService, PackageService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddHttpClient<IMomoPaymentGateway, MomoPaymentGateway>();
        services.AddHttpClient<IPayOsPaymentGateway, PayOsPaymentGateway>();

        return services;
    }
}
