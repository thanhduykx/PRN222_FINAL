using PRN222_FINAL.Models;
using PRN222_FINAL.DAL;
using PRN222_FINAL.DAL.Context;
using PRN222_FINAL.DAL.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PRN222_FINAL.BLL;

public static class BusinessServiceCollectionExtensions
{
    public static IServiceCollection AddKnowledgeBusinessServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IKnowledgeRepository>(_ =>
            new SqlKnowledgeRepository(configuration.GetConnectionString("DefaultConnection") ?? string.Empty));
        services.AddSingleton<IKnowledgeService, KnowledgeService>();

        return services;
    }
}

