using PRN222_FINAL.DAL.Entities;

namespace PRN222_FINAL.DAL.Repositories.Benchmarks;

public interface IBenchmarkRepository
{
    Task<IReadOnlyList<KnowledgeSqlExperimentRun>> GetExperimentRunsAsync(CancellationToken cancellationToken = default);
    Task<KnowledgeSqlExperimentRun?> GetExperimentRunAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnowledgeSqlBenchmarkResult>> GetResultsForRunAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<Guid> CreateRunAsync(KnowledgeSqlExperimentRun run, CancellationToken cancellationToken = default);
    Task SeedMockQuestionsAsync(string subject, CancellationToken cancellationToken = default);
    Task UpdateRunStatusAsync(Guid runId, string status, DateTimeOffset? completedAt = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnowledgeSqlTestQuestion>> GetQuestionsAsync(string subject, CancellationToken cancellationToken = default);
    Task AddResultAsync(KnowledgeSqlBenchmarkResult result, CancellationToken cancellationToken = default);
    Task UpdateRunAnalysisAsync(Guid runId, string analysisReport, CancellationToken cancellationToken = default);
}
