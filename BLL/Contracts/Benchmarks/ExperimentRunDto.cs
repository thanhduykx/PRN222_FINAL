namespace PRN222_FINAL.BLL.Contracts.Benchmarks;

public sealed record ExperimentRunDto(
    Guid Id,
    string RunName,
    string Subject,
    string ChatModel,
    string EmbeddingModel,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    double? AvgFaithfulness,
    double? AvgAnswerRelevancy,
    double? AvgContextPrecision,
    double? AvgContextRecall,
    double? AvgRagasScore,
    double? AvgLatencyMs,
    int TotalQuestions,
    string AiAnalysisReport
);
