using Npgsql;
using PRN222_FINAL.DAL.Models.Accounts;

namespace PRN222_FINAL.DAL.Repositories.Accounts;

public sealed class PostgresAccountEmailJobRepository : IAccountEmailJobRepository
{
    private readonly string _connectionString;

    public PostgresAccountEmailJobRepository(string connectionString) => _connectionString =
        !string.IsNullOrWhiteSpace(connectionString)
            ? connectionString
            : throw new InvalidOperationException("DefaultConnection is not configured.");

    public async Task EnqueueAsync(AccountEmailJobData job, CancellationToken cancellationToken = default)
    {
        await EnsureCreatedAsync(cancellationToken);
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO account_email_jobs
                ("Id","UserId","Email","FullName","Role","ApplicationBaseUrl","SubjectLabelsJson","Attempts","AvailableAt","CreatedAt")
            VALUES
                (@Id,@UserId,@Email,@FullName,@Role,@ApplicationBaseUrl,@SubjectLabelsJson,0,@AvailableAt,@CreatedAt)
            """;
        AddJobParameters(command, job);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<AccountEmailJobData?> ClaimNextAsync(
        DateTimeOffset now,
        TimeSpan lease,
        CancellationToken cancellationToken = default)
    {
        await EnsureCreatedAsync(cancellationToken);
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = """
            SELECT "Id","UserId","Email","FullName","Role","ApplicationBaseUrl","SubjectLabelsJson",
                   "Attempts","AvailableAt","LockedUntil","CompletedAt","LastError","CreatedAt"
            FROM account_email_jobs
            WHERE "CompletedAt" IS NULL
              AND "AvailableAt" <= @Now
              AND ("LockedUntil" IS NULL OR "LockedUntil" <= @Now)
              AND "Attempts" < 6
            ORDER BY "CreatedAt"
            FOR UPDATE SKIP LOCKED
            LIMIT 1
            """;
        select.Parameters.AddWithValue("@Now", now);
        AccountEmailJobData? job;
        await using (var reader = await select.ExecuteReaderAsync(cancellationToken))
        {
            job = await reader.ReadAsync(cancellationToken) ? ReadJob(reader) : null;
        }
        if (job is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        job.Attempts++;
        job.LockedUntil = now.Add(lease);
        await using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = "UPDATE account_email_jobs SET \"Attempts\"=@Attempts, \"LockedUntil\"=@LockedUntil WHERE \"Id\"=@Id";
        update.Parameters.AddWithValue("@Attempts", job.Attempts);
        update.Parameters.AddWithValue("@LockedUntil", job.LockedUntil.Value);
        update.Parameters.AddWithValue("@Id", job.Id);
        await update.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return job;
    }

    public Task CompleteAsync(Guid jobId, DateTimeOffset completedAt, CancellationToken cancellationToken = default) =>
        ExecuteUpdateAsync(
            "UPDATE account_email_jobs SET \"CompletedAt\"=@When, \"LockedUntil\"=NULL, \"LastError\"='' WHERE \"Id\"=@Id",
            jobId,
            completedAt,
            string.Empty,
            cancellationToken);

    public Task RescheduleAsync(Guid jobId, DateTimeOffset availableAt, string error, bool terminal, CancellationToken cancellationToken = default) =>
        ExecuteUpdateAsync(
            terminal
                ? "UPDATE account_email_jobs SET \"CompletedAt\"=@When, \"LockedUntil\"=NULL, \"LastError\"=@Error WHERE \"Id\"=@Id"
                : "UPDATE account_email_jobs SET \"AvailableAt\"=@When, \"LockedUntil\"=NULL, \"LastError\"=@Error WHERE \"Id\"=@Id",
            jobId,
            availableAt,
            error,
            cancellationToken);

    private async Task ExecuteUpdateAsync(string sql, Guid id, DateTimeOffset when, string error, CancellationToken cancellationToken)
    {
        await EnsureCreatedAsync(cancellationToken);
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@When", when);
        command.Parameters.AddWithValue("@Error", error.Length <= 2000 ? error : error[..2000]);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsureCreatedAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS account_email_jobs (
                "Id" UUID NOT NULL PRIMARY KEY,
                "UserId" UUID NOT NULL,
                "Email" VARCHAR(320) NOT NULL,
                "FullName" VARCHAR(120) NOT NULL,
                "Role" VARCHAR(32) NOT NULL,
                "ApplicationBaseUrl" VARCHAR(1000) NOT NULL,
                "SubjectLabelsJson" TEXT NOT NULL,
                "Attempts" INTEGER NOT NULL DEFAULT 0,
                "AvailableAt" TIMESTAMPTZ NOT NULL,
                "LockedUntil" TIMESTAMPTZ NULL,
                "CompletedAt" TIMESTAMPTZ NULL,
                "LastError" VARCHAR(2000) NOT NULL DEFAULT '',
                "CreatedAt" TIMESTAMPTZ NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_account_email_jobs_pending"
                ON account_email_jobs ("AvailableAt", "CreatedAt")
                WHERE "CompletedAt" IS NULL;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddJobParameters(NpgsqlCommand command, AccountEmailJobData job)
    {
        command.Parameters.AddWithValue("@Id", job.Id);
        command.Parameters.AddWithValue("@UserId", job.UserId);
        command.Parameters.AddWithValue("@Email", job.Email);
        command.Parameters.AddWithValue("@FullName", job.FullName);
        command.Parameters.AddWithValue("@Role", job.Role);
        command.Parameters.AddWithValue("@ApplicationBaseUrl", job.ApplicationBaseUrl);
        command.Parameters.AddWithValue("@SubjectLabelsJson", job.SubjectLabelsJson);
        command.Parameters.AddWithValue("@AvailableAt", job.AvailableAt);
        command.Parameters.AddWithValue("@CreatedAt", job.CreatedAt);
    }

    private static AccountEmailJobData ReadJob(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0), UserId = reader.GetGuid(1), Email = reader.GetString(2), FullName = reader.GetString(3),
        Role = reader.GetString(4), ApplicationBaseUrl = reader.GetString(5), SubjectLabelsJson = reader.GetString(6),
        Attempts = reader.GetInt32(7), AvailableAt = reader.GetFieldValue<DateTimeOffset>(8),
        LockedUntil = reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9),
        CompletedAt = reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10), LastError = reader.GetString(11),
        CreatedAt = reader.GetFieldValue<DateTimeOffset>(12)
    };
}
