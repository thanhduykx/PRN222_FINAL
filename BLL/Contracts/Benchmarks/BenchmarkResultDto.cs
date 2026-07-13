namespace PRN222_FINAL.BLL.Contracts.Benchmarks;

public sealed record BenchmarkResultDto(
    Guid Id,
    Guid RunId,
    Guid QuestionId,
    string GeneratedAnswer,
    double? Faithfulness,
    double? AnswerRelevancy,
    double? ContextPrecision,
    double? ContextRecall,
    double? RagasScore,
    double LatencyMs,
    DateTimeOffset EvaluatedAt,
    TestQuestionDto? Question
);
