using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PRN222_FINAL.BLL;
using PRN222_FINAL.BLL.Options;
using PRN222_FINAL.BLL.Services.Analytics;
using PRN222_FINAL.BLL.Services.Billing;
using PRN222_FINAL.BLL.Services.Billing.Gateways;
using PRN222_FINAL.BLL.Services.Documents;
using PRN222_FINAL.BLL.Services;
using PRN222_FINAL.BLL.Services.Accounts;
using PRN222_FINAL.DAL.Repositories.Accounts;
using PRN222_FINAL.BLL.Services.Email;
using PRN222_FINAL.DAL.Models.Email;
using PRN222_FINAL.DAL.Repositories.Email;
using PRN222_FINAL.DAL.Repositories.Http;
using PRN222_FINAL.BLL.Services.Chat;
using PRN222_FINAL.DAL.Repositories;
using PRN222_FINAL.DAL.Repositories.Analytics;
using PRN222_FINAL.DAL.Repositories.Billing;
using PRN222_FINAL.DAL.Repositories.Files;
using PRN222_FINAL.BLL.Models;

namespace PRN222_FINAL.BLL;

public static class BusinessServiceCollectionExtensions
{
    public static IServiceCollection AddKnowledgeBusinessServices(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;

        services.AddSingleton<IKnowledgeRepository>(_ => new SqlKnowledgeRepository(connectionString));
        services.AddSingleton<IUserAccountRepository>(_ => new PostgresUserAccountRepository(connectionString));
        var smtp = configuration.GetSection("Smtp");
        services.AddSingleton<IEmailRepository>(_ => new SmtpEmailRepository(new SmtpSettingsData(
            smtp["Host"] ?? string.Empty,
            int.TryParse(smtp["Port"], out var smtpPort) ? smtpPort : 587,
            !bool.TryParse(smtp["EnableSsl"], out var smtpSsl) || smtpSsl,
            smtp["FromEmail"] ?? string.Empty,
            smtp["FromName"] ?? "CPMS",
            smtp["UserName"] ?? string.Empty,
            smtp["Password"] ?? string.Empty)));
        services.AddSingleton<IAccountEmailService>(provider => new AccountEmailService(
            provider.GetRequiredService<IEmailRepository>(), Path.Combine(contentRootPath, "wwwroot")));
        var seedAdmin = configuration.GetSection("SeedAdmin");
        services.AddSingleton<IUserAccountService>(provider => new UserAccountService(
            provider.GetRequiredService<IUserAccountRepository>(),
            provider.GetRequiredService<IKnowledgeRepository>(),
            new SeedAdminOptions(
                !bool.TryParse(seedAdmin["Enabled"], out var enabled) || enabled,
                seedAdmin["FullName"] ?? "System Admin",
                seedAdmin["Email"] ?? "admin@eduvietrag.local",
                seedAdmin["Password"] ?? "Admin@12345")));
        services.AddSingleton<IFileRepository, LocalFileRepository>();
        services.AddSingleton<IHttpRepository>(_ => new HttpRepository(TimeSpan.FromSeconds(60)));
        services.AddSingleton<IWebPageTextExtractor, WebPageTextExtractor>();
        services.AddSingleton<IEmbeddingService>(provider =>
            (configuration["Embedding:Provider"] ?? "Hashing").Equals("Gemini", StringComparison.OrdinalIgnoreCase)
                ? new GeminiEmbeddingService(provider.GetRequiredService<IHttpRepository>(), provider.GetRequiredService<GeminiOptions>())
                : new HashingEmbeddingService());
        services.AddSingleton<IDocumentFileService, DocumentFileService>();
        services.AddSingleton<IAiSettingsService>(provider => new AiSettingsService(
            contentRootPath,
            provider.GetRequiredService<GeminiOptions>(),
            provider.GetRequiredService<ChatGenerationOptions>(),
            provider.GetRequiredService<FlmSyllabusAwareTextChunker>(),
            provider.GetRequiredService<IFileRepository>()));
        services.AddSingleton<IKnowledgeService, KnowledgeService>();

        services.Configure<PaymentOptions>(configuration.GetSection("Payment"));
        services.AddSingleton<IPackageRepository>(_ => new PackageRepository(connectionString));
        services.AddSingleton<IPaymentRepository>(_ => new PaymentRepository(connectionString));
        services.AddSingleton<ISubscriptionRepository>(_ => new SubscriptionRepository(connectionString));
        services.AddScoped<IPackageService, PackageService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddSingleton<IMomoPaymentGateway, MomoPaymentGateway>();
        services.AddSingleton<IPayOsPaymentGateway, PayOsPaymentGateway>();
        services.AddSingleton<ILocalChatCompletionService>(provider => new CompatibleChatCompletionService(
            provider.GetRequiredService<IHttpRepository>(),
            () => provider.GetRequiredService<ChatGenerationOptions>().CurrentCompatibleOptions));
        services.AddSingleton<IAnalyticsRepository>(_ => new AnalyticsRepository(connectionString));
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IChatUsageService, ChatUsageService>();

        return services;
    }
}
