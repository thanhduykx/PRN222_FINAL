using PRN222_FINAL.DAL.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace PRN222_FINAL.DAL.Schema;

public static class KnowledgeSqlSchemaInitializer
{
    public static void EnsureTablesCreated(KnowledgeSqlDbContext context)
    {
        if (context.Database.GetService<IRelationalDatabaseCreator>() is not { } creator)
        {
            return;
        }

        if (!creator.Exists())
        {
            creator.Create();
        }

        var pendingMigrations = context.Database.GetPendingMigrations();
        if (pendingMigrations.Any())
        {
            context.Database.Migrate();
            return;
        }

        if (!HasTable(context, "rag_subjects"))
        {
            creator.CreateTables();
        }
    }

    private static bool HasTable(KnowledgeSqlDbContext context, string tableName)
    {
        return context.Database
            .SqlQueryRaw<int>(
                "SELECT CASE WHEN to_regclass({0}) IS NULL THEN 0 ELSE 1 END AS \"Value\"",
                $"public.{tableName}")
            .Single() == 1;
    }
}
