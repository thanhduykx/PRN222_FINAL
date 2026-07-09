using Microsoft.EntityFrameworkCore;

namespace PRN222_FINAL.DAL.Context;

public static class KnowledgeSqlDbContextOptionsFactory
{
    public static DbContextOptions<KnowledgeSqlDbContext> Create(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("DefaultConnection is not configured.");
        }

        return new DbContextOptionsBuilder<KnowledgeSqlDbContext>()
            .UseNpgsql(connectionString, options => options.CommandTimeout(15))
            .Options;
    }
}
