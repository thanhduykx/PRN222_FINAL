using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PRN222_FINAL.DAL.Entities;

[Table("rag_benchmark_results")]
public sealed class KnowledgeSqlBenchmarkResult
{
    [Key]
    public Guid Id { get; set; }

    public Guid RunId { get; set; }
    public Guid QuestionId { get; set; }

    [Required]
    public string GeneratedAnswer { get; set; } = string.Empty;

    public double? Faithfulness { get; set; }
    public double? AnswerRelevancy { get; set; }
    public double? ContextPrecision { get; set; }
    public double? ContextRecall { get; set; }
    public double? RagasScore { get; set; }

    public double LatencyMs { get; set; }

    public DateTimeOffset EvaluatedAt { get; set; } = DateTimeOffset.UtcNow;

    public KnowledgeSqlExperimentRun Run { get; set; } = null!;
    public KnowledgeSqlTestQuestion Question { get; set; } = null!;
}
