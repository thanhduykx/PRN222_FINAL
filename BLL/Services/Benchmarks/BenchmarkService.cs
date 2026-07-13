using PRN222_FINAL.BLL.Contracts.Benchmarks;
using PRN222_FINAL.DAL.Entities;
using PRN222_FINAL.DAL.Repositories.Benchmarks;

namespace PRN222_FINAL.BLL.Services.Benchmarks;

public sealed class BenchmarkService : IBenchmarkService
{
    private readonly IBenchmarkRepository _repository;
    private readonly IBenchmarkJobQueue _jobQueue;

    public BenchmarkService(IBenchmarkRepository repository, IBenchmarkJobQueue jobQueue)
    {
        _repository = repository;
        _jobQueue = jobQueue;
    }

    public async Task<IReadOnlyList<ExperimentRunDto>> GetExperimentRunsAsync(CancellationToken cancellationToken = default)
    {
        var runs = await _repository.GetExperimentRunsAsync(cancellationToken);
        return runs.Select(MapToDto).ToList();
    }

    public async Task<ExperimentRunDto?> GetExperimentRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await _repository.GetExperimentRunAsync(runId, cancellationToken);
        return run is null ? null : MapToDto(run);
    }

    public async Task<IReadOnlyList<BenchmarkResultDto>> GetResultsForRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var results = await _repository.GetResultsForRunAsync(runId, cancellationToken);
        return results.Select(r => new BenchmarkResultDto(
            r.Id,
            r.RunId,
            r.QuestionId,
            r.GeneratedAnswer,
            r.Faithfulness,
            r.AnswerRelevancy,
            r.ContextPrecision,
            r.ContextRecall,
            r.RagasScore,
            r.LatencyMs,
            r.EvaluatedAt,
            new TestQuestionDto(
                r.Question.Id,
                r.Question.Subject,
                r.Question.Question,
                r.Question.GroundTruth,
                r.Question.Difficulty,
                r.Question.Category,
                r.Question.CreatedAt
            )
        )).ToList();
    }

    public async Task<Guid> StartBenchmarkAsync(string subject, string chatModel, string embeddingModel, CancellationToken cancellationToken = default)
    {
        var run = new KnowledgeSqlExperimentRun
        {
            Id = Guid.NewGuid(),
            RunName = $"Benchmark {DateTimeOffset.Now:yyyyMMdd-HHmm}",
            Subject = subject,
            ChatModel = chatModel,
            EmbeddingModel = embeddingModel,
            Status = "Pending",
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _repository.CreateRunAsync(run, cancellationToken);
        await _jobQueue.EnqueueBenchmarkAsync(run.Id, cancellationToken);

        return run.Id;
    }

    public async Task SeedMockQuestionsAsync(string subject, CancellationToken cancellationToken = default)
    {
        await _repository.SeedMockQuestionsAsync(subject, cancellationToken);
    }

    private static ExperimentRunDto MapToDto(KnowledgeSqlExperimentRun run)
    {
        var results = run.Results.ToList();
        var total = results.Count;
        return new ExperimentRunDto(
            run.Id,
            run.RunName,
            run.Subject,
            run.ChatModel,
            run.EmbeddingModel,
            run.Status,
            run.CreatedAt,
            run.CompletedAt,
            total > 0 ? results.Average(r => r.Faithfulness ?? 0) : null,
            total > 0 ? results.Average(r => r.AnswerRelevancy ?? 0) : null,
            total > 0 ? results.Average(r => r.ContextPrecision ?? 0) : null,
            total > 0 ? results.Average(r => r.ContextRecall ?? 0) : null,
            total > 0 ? results.Average(r => r.RagasScore ?? 0) : null,
            total > 0 ? results.Average(r => r.LatencyMs) : null,
            total,
            run.AiAnalysisReport
        );
    }
}
