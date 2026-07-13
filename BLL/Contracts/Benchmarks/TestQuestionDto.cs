namespace PRN222_FINAL.BLL.Contracts.Benchmarks;

public sealed record TestQuestionDto(
    Guid Id,
    string Subject,
    string Question,
    string GroundTruth,
    string Difficulty,
    string? Category,
    DateTimeOffset CreatedAt
);
