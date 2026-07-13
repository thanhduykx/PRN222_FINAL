using Microsoft.EntityFrameworkCore;
using PRN222_FINAL.DAL.Context;
using PRN222_FINAL.DAL.Entities;

namespace PRN222_FINAL.DAL.Repositories.Benchmarks;

public sealed class SqlBenchmarkRepository : IBenchmarkRepository
{
    private readonly DbContextOptions<KnowledgeSqlDbContext> _options;

    public SqlBenchmarkRepository(string connectionString)
    {
        _options = KnowledgeSqlDbContextOptionsFactory.Create(connectionString);
    }

    private KnowledgeSqlDbContext CreateContext() => new(_options);

    public async Task<IReadOnlyList<KnowledgeSqlExperimentRun>> GetExperimentRunsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.ExperimentRuns
            .Include(r => r.Results)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<KnowledgeSqlExperimentRun?> GetExperimentRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.ExperimentRuns
            .Include(r => r.Results)
            .FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
    }

    public async Task<IReadOnlyList<KnowledgeSqlBenchmarkResult>> GetResultsForRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.BenchmarkResults
            .Include(r => r.Question)
            .Where(r => r.RunId == runId)
            .OrderBy(r => r.EvaluatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid> CreateRunAsync(KnowledgeSqlExperimentRun run, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        context.ExperimentRuns.Add(run);
        await context.SaveChangesAsync(cancellationToken);
        return run.Id;
    }

    public async Task SeedMockQuestionsAsync(string subject, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var hasQuestions = await context.TestQuestions.AnyAsync(q => q.Subject == subject, cancellationToken);
        if (hasQuestions) return;

        var questions = new List<KnowledgeSqlTestQuestion>();
        if (subject.Contains("DBA103", StringComparison.OrdinalIgnoreCase))
        {
            questions.Add(new() { Id = Guid.NewGuid(), Subject = subject, Question = "Môn DBA103 có bao nhiêu tín chỉ?", GroundTruth = "DBA103 là môn học 3 tín chỉ." });
            questions.Add(new() { Id = Guid.NewGuid(), Subject = subject, Question = "Môn học DBA103 tập trung vào nhạc cụ nào?", GroundTruth = "Môn học tập trung vào Đàn Bầu." });
            questions.Add(new() { Id = Guid.NewGuid(), Subject = subject, Question = "Môn học có bao nhiêu slot trên lớp?", GroundTruth = "Môn học có 30 slots trên lớp." });
            questions.Add(new() { Id = Guid.NewGuid(), Subject = subject, Question = "Điểm đạt môn tối thiểu là bao nhiêu?", GroundTruth = "Điểm đạt môn tối thiểu là 5/10." });
            questions.Add(new() { Id = Guid.NewGuid(), Subject = subject, Question = "Quy mô lớp học khoảng bao nhiêu sinh viên?", GroundTruth = "Khoảng 15 sinh viên/lớp." });
        }
        else if (subject.Contains("IOT102", StringComparison.OrdinalIgnoreCase))
        {
            questions.Add(new() { Id = Guid.NewGuid(), Subject = subject, Question = "Môn IOT102 nói về chủ đề gì?", GroundTruth = "Nói về Internet of Things (IoT)." });
            questions.Add(new() { Id = Guid.NewGuid(), Subject = subject, Question = "Tổng quan môn học trang bị kiến thức gì?", GroundTruth = "Trang bị kiến thức cơ bản về phần cứng, cảm biến và kết nối." });
            questions.Add(new() { Id = Guid.NewGuid(), Subject = subject, Question = "Điều kiện tiên quyết của môn này là gì?", GroundTruth = "Không có môn tiên quyết (hoặc yêu cầu biết lập trình cơ bản)." });
            questions.Add(new() { Id = Guid.NewGuid(), Subject = subject, Question = "Môn học IOT102 có bao nhiêu tín chỉ?", GroundTruth = "Thường là 3 tín chỉ." });
            questions.Add(new() { Id = Guid.NewGuid(), Subject = subject, Question = "Sinh viên cần làm đồ án cuối kỳ không?", GroundTruth = "Có, sinh viên phải thực hiện mini project." });
        }
        else
        {
            questions.Add(new() { Id = Guid.NewGuid(), Subject = subject, Question = "What is RAG?", GroundTruth = "RAG stands for Retrieval-Augmented Generation." });
            questions.Add(new() { Id = Guid.NewGuid(), Subject = subject, Question = "How does vector search work?", GroundTruth = "It finds nearest neighbors in a high-dimensional space." });
            questions.Add(new() { Id = Guid.NewGuid(), Subject = subject, Question = "What are embeddings?", GroundTruth = "Embeddings are numeric representations of text." });
            questions.Add(new() { Id = Guid.NewGuid(), Subject = subject, Question = "Why use chunking?", GroundTruth = "To fit documents into the LLM context window." });
            questions.Add(new() { Id = Guid.NewGuid(), Subject = subject, Question = "What is Faithfulness in Ragas?", GroundTruth = "It measures if the answer is grounded in the retrieved context." });
        }

        context.TestQuestions.AddRange(questions);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRunStatusAsync(Guid runId, string status, DateTimeOffset? completedAt = null, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var run = await context.ExperimentRuns.FindAsync(new object[] { runId }, cancellationToken);
        if (run != null)
        {
            run.Status = status;
            if (completedAt.HasValue) run.CompletedAt = completedAt;
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<KnowledgeSqlTestQuestion>> GetQuestionsAsync(string subject, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.TestQuestions.Where(q => q.Subject == subject).ToListAsync(cancellationToken);
    }

    public async Task AddResultAsync(KnowledgeSqlBenchmarkResult result, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        context.BenchmarkResults.Add(result);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRunAnalysisAsync(Guid runId, string analysisReport, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var run = await context.ExperimentRuns.FindAsync(new object[] { runId }, cancellationToken);
        if (run != null)
        {
            run.AiAnalysisReport = analysisReport;
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
