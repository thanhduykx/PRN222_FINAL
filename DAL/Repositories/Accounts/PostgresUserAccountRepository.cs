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
                   "PasswordResetTokenExpiresAt", "PasswordChangedAt", "Provider", "Role", "CreatedAt", "LastActiveAt",
                   "IsSuspended", "SuspendedAt", "FailedLoginCount", "LockoutEnd"
            FROM app_users ORDER BY "Email"
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            users.Add(new UserAccountData { Id=reader.GetGuid(0),Email=reader.GetString(1),FullName=reader.GetString(2),PasswordHash=reader.GetString(3),PasswordResetTokenHash=reader.IsDBNull(4)?null:reader.GetString(4),PasswordResetTokenExpiresAt=reader.IsDBNull(5)?null:reader.GetFieldValue<DateTimeOffset>(5),PasswordChangedAt=reader.IsDBNull(6)?null:reader.GetFieldValue<DateTimeOffset>(6),Provider=reader.GetString(7),Role=reader.GetString(8),CreatedAt=reader.GetFieldValue<DateTimeOffset>(9),LastActiveAt=reader.IsDBNull(10)?null:reader.GetFieldValue<DateTimeOffset>(10),IsSuspended=reader.GetBoolean(11),SuspendedAt=reader.IsDBNull(12)?null:reader.GetFieldValue<DateTimeOffset>(12),FailedLoginCount=reader.GetInt32(13),LockoutEnd=reader.IsDBNull(14)?null:reader.GetFieldValue<DateTimeOffset>(14) });
        return users;
    }

    public async Task UpsertAsync(UserAccountData user, CancellationToken cancellationToken = default)
    {
        await EnsureCreatedAsync(cancellationToken);
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO app_users ("Id","Email","FullName","PasswordHash","PasswordResetTokenHash","PasswordResetTokenExpiresAt","PasswordChangedAt","Provider","Role","CreatedAt","LastActiveAt","IsSuspended","SuspendedAt","FailedLoginCount","LockoutEnd")
            VALUES (@Id,@Email,@FullName,@PasswordHash,@PasswordResetTokenHash,@PasswordResetTokenExpiresAt,@PasswordChangedAt,@Provider,@Role,@CreatedAt,@LastActiveAt,@IsSuspended,@SuspendedAt,@FailedLoginCount,@LockoutEnd)
            ON CONFLICT ("Id") DO UPDATE SET
                "Email" = EXCLUDED."Email",
                "FullName" = EXCLUDED."FullName",
                "PasswordHash" = EXCLUDED."PasswordHash",
                "PasswordResetTokenHash" = EXCLUDED."PasswordResetTokenHash",
                "PasswordResetTokenExpiresAt" = EXCLUDED."PasswordResetTokenExpiresAt",
                "PasswordChangedAt" = EXCLUDED."PasswordChangedAt",
                "Provider" = EXCLUDED."Provider",
                "Role" = EXCLUDED."Role"
            """;
        AddParameters(command, user);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsureCreatedAsync(cancellationToken);
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM app_users WHERE \"Id\" = @Id";
        command.Parameters.AddWithValue("@Id", userId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException("User not found.");
        }
    }

    public async Task UpdateLastActiveAsync(Guid userId, DateTimeOffset activeAt, CancellationToken cancellationToken = default)
    {
        await EnsureCreatedAsync(cancellationToken);
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE app_users SET \"LastActiveAt\" = @LastActiveAt WHERE \"Id\" = @Id";
        command.Parameters.AddWithValue("@Id", userId);
        command.Parameters.AddWithValue("@LastActiveAt", activeAt);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            throw new InvalidOperationException("User not found.");
    }

    public async Task RecordLoginFailureAsync(Guid userId, int maxFailures, DateTimeOffset lockoutEnd, CancellationToken cancellationToken = default)
    {
        await ExecuteUserUpdateAsync(
            """
            UPDATE app_users
            SET "FailedLoginCount" = "FailedLoginCount" + 1,
                "LockoutEnd" = CASE WHEN "FailedLoginCount" + 1 >= @MaxFailures THEN @LockoutEnd ELSE "LockoutEnd" END
            WHERE "Id" = @Id
            """,
            userId,
            command =>
            {
                command.Parameters.AddWithValue("@MaxFailures", Math.Max(1, maxFailures));
                command.Parameters.AddWithValue("@LockoutEnd", lockoutEnd);
            },
            cancellationToken);
    }

    public Task ResetLoginFailuresAsync(Guid userId, CancellationToken cancellationToken = default) =>
        ExecuteUserUpdateAsync(
            "UPDATE app_users SET \"FailedLoginCount\" = 0, \"LockoutEnd\" = NULL WHERE \"Id\" = @Id",
            userId,
            null,
            cancellationToken);

    public Task SetSuspendedAsync(Guid userId, bool isSuspended, DateTimeOffset changedAt, CancellationToken cancellationToken = default) =>
        ExecuteUserUpdateAsync(
            "UPDATE app_users SET \"IsSuspended\" = @IsSuspended, \"SuspendedAt\" = CASE WHEN @IsSuspended THEN @ChangedAt ELSE NULL END WHERE \"Id\" = @Id",
            userId,
            command =>
            {
                command.Parameters.AddWithValue("@IsSuspended", isSuspended);
                command.Parameters.AddWithValue("@ChangedAt", changedAt);
            },
            cancellationToken);

    private async Task ExecuteUserUpdateAsync(string sql, Guid userId, Action<NpgsqlCommand>? addParameters, CancellationToken cancellationToken)
    {
        await EnsureCreatedAsync(cancellationToken);
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@Id", userId);
        addParameters?.Invoke(command);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException("User not found.");
        }
    }

    private async Task EnsureCreatedAsync(CancellationToken cancellationToken)
    {
        await using var connection=new NpgsqlConnection(_connectionString); await connection.OpenAsync(cancellationToken);
        await using var command=connection.CreateCommand(); command.CommandText="""
            CREATE TABLE IF NOT EXISTS app_users ("Id" UUID NOT NULL CONSTRAINT "PK_app_users" PRIMARY KEY,"Email" VARCHAR(320) NOT NULL,"FullName" VARCHAR(120) NOT NULL,"PasswordHash" VARCHAR(512) NOT NULL,"PasswordResetTokenHash" VARCHAR(128) NULL,"PasswordResetTokenExpiresAt" TIMESTAMPTZ NULL,"PasswordChangedAt" TIMESTAMPTZ NULL,"Provider" VARCHAR(120) NOT NULL,"Role" VARCHAR(32) NOT NULL,"CreatedAt" TIMESTAMPTZ NOT NULL);
            ALTER TABLE app_users ADD COLUMN IF NOT EXISTS "PasswordResetTokenHash" VARCHAR(128) NULL;
            ALTER TABLE app_users ADD COLUMN IF NOT EXISTS "PasswordResetTokenExpiresAt" TIMESTAMPTZ NULL;
            ALTER TABLE app_users ADD COLUMN IF NOT EXISTS "PasswordChangedAt" TIMESTAMPTZ NULL;
            ALTER TABLE app_users ADD COLUMN IF NOT EXISTS "LastActiveAt" TIMESTAMPTZ NULL;
            ALTER TABLE app_users ADD COLUMN IF NOT EXISTS "IsSuspended" BOOLEAN NOT NULL DEFAULT FALSE;
            ALTER TABLE app_users ADD COLUMN IF NOT EXISTS "SuspendedAt" TIMESTAMPTZ NULL;
            ALTER TABLE app_users ADD COLUMN IF NOT EXISTS "FailedLoginCount" INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE app_users ADD COLUMN IF NOT EXISTS "LockoutEnd" TIMESTAMPTZ NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS "UX_app_users_Email" ON app_users ("Email"); CREATE INDEX IF NOT EXISTS "IX_app_users_Role" ON app_users ("Role");
            """; await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameters(NpgsqlCommand c, UserAccountData u)
    { c.Parameters.AddWithValue("@Id",u.Id);c.Parameters.AddWithValue("@Email",u.Email);c.Parameters.AddWithValue("@FullName",u.FullName);c.Parameters.AddWithValue("@PasswordHash",u.PasswordHash);c.Parameters.AddWithValue("@PasswordResetTokenHash",(object?)u.PasswordResetTokenHash??DBNull.Value);c.Parameters.AddWithValue("@PasswordResetTokenExpiresAt",(object?)u.PasswordResetTokenExpiresAt??DBNull.Value);c.Parameters.AddWithValue("@PasswordChangedAt",(object?)u.PasswordChangedAt??DBNull.Value);c.Parameters.AddWithValue("@Provider",u.Provider);c.Parameters.AddWithValue("@Role",u.Role);c.Parameters.AddWithValue("@CreatedAt",u.CreatedAt);c.Parameters.AddWithValue("@LastActiveAt",(object?)u.LastActiveAt??DBNull.Value);c.Parameters.AddWithValue("@IsSuspended",u.IsSuspended);c.Parameters.AddWithValue("@SuspendedAt",(object?)u.SuspendedAt??DBNull.Value);c.Parameters.AddWithValue("@FailedLoginCount",u.FailedLoginCount);c.Parameters.AddWithValue("@LockoutEnd",(object?)u.LockoutEnd??DBNull.Value); }
}
