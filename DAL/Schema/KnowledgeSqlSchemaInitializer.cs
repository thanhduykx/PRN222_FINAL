using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using PRN222_FINAL.DAL.Context;

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
            EnsureBillingTablesCreated(context);
            return;
        }

        if (!HasTable(context, "rag_subjects"))
        {
            creator.CreateTables();
        }

        EnsureBillingTablesCreated(context);
        EnsureBenchmarkTablesCreated(context);
    }

    public static void EnsureBenchmarkTablesCreated(KnowledgeSqlDbContext context)
    {
        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS rag_test_questions (
                "Id" uuid PRIMARY KEY,
                "Subject" varchar(255) NOT NULL,
                "Question" text NOT NULL,
                "GroundTruth" text NOT NULL,
                "Difficulty" varchar(64) NULL,
                "Category" varchar(128) NULL,
                "CreatedAt" timestamp with time zone NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_rag_test_questions_Subject" ON rag_test_questions ("Subject");

            CREATE TABLE IF NOT EXISTS rag_experiment_runs (
                "Id" uuid PRIMARY KEY,
                "RunName" varchar(255) NOT NULL,
                "Subject" varchar(255) NOT NULL,
                "ChatModel" varchar(128) NOT NULL,
                "EmbeddingModel" varchar(128) NOT NULL,
                "Status" varchar(64) NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "CompletedAt" timestamp with time zone NULL,
                "AiAnalysisReport" text NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS "IX_rag_experiment_runs_Subject" ON rag_experiment_runs ("Subject");
            ALTER TABLE rag_experiment_runs ADD COLUMN IF NOT EXISTS "AiAnalysisReport" text NOT NULL DEFAULT '';

            CREATE TABLE IF NOT EXISTS rag_benchmark_results (
                "Id" uuid PRIMARY KEY,
                "RunId" uuid NOT NULL REFERENCES rag_experiment_runs ("Id") ON DELETE CASCADE,
                "QuestionId" uuid NOT NULL REFERENCES rag_test_questions ("Id") ON DELETE CASCADE,
                "GeneratedAnswer" text NOT NULL,
                "Faithfulness" double precision NULL,
                "AnswerRelevancy" double precision NULL,
                "ContextPrecision" double precision NULL,
                "ContextRecall" double precision NULL,
                "RagasScore" double precision NULL,
                "LatencyMs" integer NOT NULL,
                "EvaluatedAt" timestamp with time zone NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_rag_benchmark_results_RunId" ON rag_benchmark_results ("RunId");
            CREATE INDEX IF NOT EXISTS "IX_rag_benchmark_results_QuestionId" ON rag_benchmark_results ("QuestionId");
            """);
    }

    public static void EnsureBillingTablesCreated(KnowledgeSqlDbContext context)
    {
        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS packages (
                "Id" uuid PRIMARY KEY,
                "Code" varchar(64) NOT NULL,
                "Name" varchar(120) NOT NULL,
                "Description" varchar(1000) NOT NULL,
                "PriceVnd" numeric(18,2) NOT NULL,
                "DurationDays" integer NOT NULL,
                "MonthlyChatLimit" integer NOT NULL,
                "MonthlyDocumentUploadLimit" integer NOT NULL,
                "StorageLimitMb" integer NOT NULL,
                "IsLifetime" boolean NOT NULL DEFAULT false,
                "IsActive" boolean NOT NULL,
                "SortOrder" integer NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_packages_Code" ON packages ("Code");
            CREATE INDEX IF NOT EXISTS "IX_packages_IsActive" ON packages ("IsActive");
            CREATE INDEX IF NOT EXISTS "IX_packages_SortOrder" ON packages ("SortOrder");
            ALTER TABLE packages ADD COLUMN IF NOT EXISTS "IsLifetime" boolean NOT NULL DEFAULT false;

            CREATE TABLE IF NOT EXISTS package_price_changes (
                "Id" uuid PRIMARY KEY,
                "PackageId" uuid NOT NULL REFERENCES packages ("Id") ON DELETE RESTRICT,
                "PackageName" varchar(120) NOT NULL,
                "OldPriceVnd" numeric(18,2) NOT NULL,
                "NewPriceVnd" numeric(18,2) NOT NULL,
                "ChangedBy" varchar(255) NOT NULL,
                "ChangedAt" timestamp with time zone NOT NULL
            );

            CREATE INDEX IF NOT EXISTS "IX_package_price_changes_ChangedAt" ON package_price_changes ("ChangedAt");
            CREATE INDEX IF NOT EXISTS "IX_package_price_changes_PackageId" ON package_price_changes ("PackageId");

            CREATE TABLE IF NOT EXISTS payments (
                "Id" uuid PRIMARY KEY,
                "PackageId" uuid NOT NULL REFERENCES packages ("Id") ON DELETE RESTRICT,
                "UserId" uuid NOT NULL,
                "UserName" varchar(255) NOT NULL,
                "UserEmail" varchar(255) NOT NULL,
                "Provider" varchar(32) NOT NULL,
                "Status" varchar(32) NOT NULL,
                "AmountVnd" numeric(18,2) NOT NULL,
                "Currency" varchar(8) NOT NULL,
                "OrderCode" varchar(80) NOT NULL,
                "ProviderTransactionId" varchar(255) NOT NULL DEFAULT '',
                "CheckoutUrl" varchar(2000) NOT NULL DEFAULT '',
                "QrCode" varchar(4000) NOT NULL DEFAULT '',
                "RawRequest" text NOT NULL DEFAULT '',
                "RawResponse" text NOT NULL DEFAULT '',
                "RawWebhook" text NOT NULL DEFAULT '',
                "CreatedAt" timestamp with time zone NOT NULL,
                "PaidAt" timestamp with time zone NULL,
                "FailedAt" timestamp with time zone NULL,
                "FailureReason" varchar(1000) NOT NULL DEFAULT ''
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_payments_Provider_OrderCode" ON payments ("Provider", "OrderCode");
            CREATE INDEX IF NOT EXISTS "IX_payments_UserId" ON payments ("UserId");
            CREATE INDEX IF NOT EXISTS "IX_payments_Status" ON payments ("Status");

            CREATE TABLE IF NOT EXISTS subscriptions (
                "Id" uuid PRIMARY KEY,
                "PackageId" uuid NOT NULL REFERENCES packages ("Id") ON DELETE RESTRICT,
                "UserId" uuid NOT NULL,
                "UserName" varchar(255) NOT NULL,
                "UserEmail" varchar(255) NOT NULL,
                "Status" varchar(32) NOT NULL,
                "StartsAt" timestamp with time zone NOT NULL,
                "EndsAt" timestamp with time zone NOT NULL,
                "PaymentId" uuid NULL REFERENCES payments ("Id") ON DELETE RESTRICT,
                "CreatedAt" timestamp with time zone NOT NULL
            );

            CREATE INDEX IF NOT EXISTS "IX_subscriptions_UserId_Status_EndsAt" ON subscriptions ("UserId", "Status", "EndsAt");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_subscriptions_PaymentId" ON subscriptions ("PaymentId") WHERE "PaymentId" IS NOT NULL;

            CREATE TABLE IF NOT EXISTS course_access_logs (
                "Id" uuid PRIMARY KEY,
                "UserId" uuid NULL,
                "UserName" varchar(255) NOT NULL DEFAULT '',
                "UserEmail" varchar(255) NOT NULL DEFAULT '',
                "Role" varchar(64) NOT NULL DEFAULT '',
                "SubjectId" uuid NOT NULL,
                "SubjectCode" varchar(64) NOT NULL DEFAULT '',
                "SubjectName" varchar(255) NOT NULL DEFAULT '',
                "AccessArea" varchar(64) NOT NULL DEFAULT '',
                "AccessedAt" timestamp with time zone NOT NULL
            );

            CREATE INDEX IF NOT EXISTS "IX_course_access_logs_UserId" ON course_access_logs ("UserId");
            CREATE INDEX IF NOT EXISTS "IX_course_access_logs_SubjectId" ON course_access_logs ("SubjectId");
            CREATE INDEX IF NOT EXISTS "IX_course_access_logs_AccessedAt" ON course_access_logs ("AccessedAt");
            """);
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
