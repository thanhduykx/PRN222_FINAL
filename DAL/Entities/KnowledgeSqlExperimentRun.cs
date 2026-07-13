using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PRN222_FINAL.DAL.Entities;

[Table("rag_experiment_runs")]
public sealed class KnowledgeSqlExperimentRun
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(255)]
    public string RunName { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string Subject { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ChatModel { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string EmbeddingModel { get; set; } = string.Empty;

    [Required, MaxLength(32)]
    public string Status { get; set; } = "Pending";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }

    public string AiAnalysisReport { get; set; } = string.Empty;

    public ICollection<KnowledgeSqlBenchmarkResult> Results { get; set; } = new List<KnowledgeSqlBenchmarkResult>();
}
