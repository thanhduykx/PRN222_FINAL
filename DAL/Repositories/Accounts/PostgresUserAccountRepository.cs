using Npgsql;
using PRN222_FINAL.DAL.Models.Accounts;

namespace PRN222_FINAL.DAL.Repositories.Accounts;

public sealed class PostgresUserAccountRepository : IUserAccountRepository
{
    private readonly string _connectionString;
    public PostgresUserAccountRepository(string connectionString) => _connectionString =
        !string.IsNullOrWhiteSpace(connectionString) ? connectionString : throw new InvalidOperationException("DefaultConnection is not configured.");

    public async Task<List<UserAccountData>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCreatedAsync(cancellationToken);
        var users = new List<UserAccountData>();
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT "Id", "Email", "FullName", "PasswordHash", "PasswordResetTokenHash",
                   "PasswordResetTokenExpiresAt", "PasswordChangedAt", "Provider", "Role", "CreatedAt"
            FROM app_users ORDER BY "Email"
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            users.Add(new UserAccountData { Id=reader.GetGuid(0),Email=reader.GetString(1),FullName=reader.GetString(2),PasswordHash=reader.GetString(3),PasswordResetTokenHash=reader.IsDBNull(4)?null:reader.GetString(4),PasswordResetTokenExpiresAt=reader.IsDBNull(5)?null:reader.GetFieldValue<DateTimeOffset>(5),PasswordChangedAt=reader.IsDBNull(6)?null:reader.GetFieldValue<DateTimeOffset>(6),Provider=reader.GetString(7),Role=reader.GetString(8),CreatedAt=reader.GetFieldValue<DateTimeOffset>(9) });
        return users;
    }

    public async Task SaveAllAsync(IReadOnlyCollection<UserAccountData> users, CancellationToken cancellationToken = default)
    {
        await EnsureCreatedAsync(cancellationToken);
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (var delete = connection.CreateCommand()) { delete.Transaction=transaction; delete.CommandText="DELETE FROM app_users"; await delete.ExecuteNonQueryAsync(cancellationToken); }
            foreach (var user in users)
            {
                await using var command=connection.CreateCommand(); command.Transaction=transaction;
                command.CommandText="""INSERT INTO app_users ("Id","Email","FullName","PasswordHash","PasswordResetTokenHash","PasswordResetTokenExpiresAt","PasswordChangedAt","Provider","Role","CreatedAt") VALUES (@Id,@Email,@FullName,@PasswordHash,@PasswordResetTokenHash,@PasswordResetTokenExpiresAt,@PasswordChangedAt,@Provider,@Role,@CreatedAt)""";
                AddParameters(command,user); await command.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }
    }

    private async Task EnsureCreatedAsync(CancellationToken cancellationToken)
    {
        await using var connection=new NpgsqlConnection(_connectionString); await connection.OpenAsync(cancellationToken);
        await using var command=connection.CreateCommand(); command.CommandText="""
            CREATE TABLE IF NOT EXISTS app_users ("Id" UUID NOT NULL CONSTRAINT "PK_app_users" PRIMARY KEY,"Email" VARCHAR(320) NOT NULL,"FullName" VARCHAR(120) NOT NULL,"PasswordHash" VARCHAR(512) NOT NULL,"PasswordResetTokenHash" VARCHAR(128) NULL,"PasswordResetTokenExpiresAt" TIMESTAMPTZ NULL,"PasswordChangedAt" TIMESTAMPTZ NULL,"Provider" VARCHAR(120) NOT NULL,"Role" VARCHAR(32) NOT NULL,"CreatedAt" TIMESTAMPTZ NOT NULL);
            ALTER TABLE app_users ADD COLUMN IF NOT EXISTS "PasswordResetTokenHash" VARCHAR(128) NULL;
            ALTER TABLE app_users ADD COLUMN IF NOT EXISTS "PasswordResetTokenExpiresAt" TIMESTAMPTZ NULL;
            ALTER TABLE app_users ADD COLUMN IF NOT EXISTS "PasswordChangedAt" TIMESTAMPTZ NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS "UX_app_users_Email" ON app_users ("Email"); CREATE INDEX IF NOT EXISTS "IX_app_users_Role" ON app_users ("Role");
            """; await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameters(NpgsqlCommand c, UserAccountData u)
    { c.Parameters.AddWithValue("@Id",u.Id);c.Parameters.AddWithValue("@Email",u.Email);c.Parameters.AddWithValue("@FullName",u.FullName);c.Parameters.AddWithValue("@PasswordHash",u.PasswordHash);c.Parameters.AddWithValue("@PasswordResetTokenHash",(object?)u.PasswordResetTokenHash??DBNull.Value);c.Parameters.AddWithValue("@PasswordResetTokenExpiresAt",(object?)u.PasswordResetTokenExpiresAt??DBNull.Value);c.Parameters.AddWithValue("@PasswordChangedAt",(object?)u.PasswordChangedAt??DBNull.Value);c.Parameters.AddWithValue("@Provider",u.Provider);c.Parameters.AddWithValue("@Role",u.Role);c.Parameters.AddWithValue("@CreatedAt",u.CreatedAt); }
}
