using Microsoft.EntityFrameworkCore;
using PRN222_FINAL.DAL.Context;
using PRN222_FINAL.DAL.Schema;

namespace PRN222_FINAL.DAL.Repositories.Billing;

public abstract class SqlBillingRepositoryBase
{
    private readonly object _schemaGate = new();
    private bool _schemaEnsured;

    protected SqlBillingRepositoryBase(string connectionString)
    {
        Options = KnowledgeSqlDbContextOptionsFactory.Create(connectionString);
    }

    protected DbContextOptions<KnowledgeSqlDbContext> Options { get; }

    protected KnowledgeSqlDbContext CreateContext()
    {
        EnsureSchema();
        return new KnowledgeSqlDbContext(Options);
    }

    private void EnsureSchema()
    {
        if (_schemaEnsured)
        {
            return;
        }

        lock (_schemaGate)
        {
            if (_schemaEnsured)
            {
                return;
            }

            using var context = new KnowledgeSqlDbContext(Options);
            KnowledgeSqlSchemaInitializer.EnsureTablesCreated(context);
            _schemaEnsured = true;
        }
    }
}
