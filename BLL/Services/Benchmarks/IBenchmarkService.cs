using PRN222_FINAL.BLL.Contracts.Benchmarks;

namespace PRN222_FINAL.BLL.Services.Benchmarks;

public interface IBenchmarkService
{
    Task<IReadOnlyList<ExperimentRunDto>> GetExperimentRunsAsync(CancellationToken cancellationToken = default);
    Task<ExperimentRunDto?> GetExperimentRunAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BenchmarkResultDto>> GetResultsForRunAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<Guid> StartBenchmarkAsync(string subject, string chatModel, string embeddingModel, CancellationToken cancellationToken = default);
    Task SeedMockQuestionsAsync(string subject, CancellationToken cancellationToken = default);
}
