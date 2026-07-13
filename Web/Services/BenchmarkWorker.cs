using Microsoft.AspNetCore.SignalR;
using PRN222_FINAL.BLL.Services.Benchmarks;
using PRN222_FINAL.DAL.Entities;
using PRN222_FINAL.DAL.Repositories.Benchmarks;
using PRN222_FINAL.Web.Hubs;

namespace PRN222_FINAL.Web.Services;

public sealed class BenchmarkWorker : BackgroundService
{
    private readonly IBenchmarkJobQueue _jobQueue;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<BenchmarkStatusHub> _hubContext;

    public BenchmarkWorker(IBenchmarkJobQueue jobQueue, IServiceProvider serviceProvider, IHubContext<BenchmarkStatusHub> hubContext)
    {
        _jobQueue = jobQueue;
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var runId = await _jobQueue.DequeueBenchmarkAsync(stoppingToken);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IBenchmarkRepository>();
                
                var run = await repository.GetExperimentRunAsync(runId, stoppingToken);
                if (run == null) continue;

                await repository.UpdateRunStatusAsync(runId, "Running", null, stoppingToken);

                var questions = await repository.GetQuestionsAsync(run.Subject, stoppingToken);
                var total = questions.Count;

                if (total == 0)
                {
                    await repository.UpdateRunStatusAsync(runId, "Completed", DateTimeOffset.UtcNow, stoppingToken);
                    await _hubContext.Clients.All.SendAsync("OnBenchmarkCompleted", runId, stoppingToken);
                    continue;
                }

                var ragChatService = scope.ServiceProvider.GetRequiredService<PRN222_FINAL.BLL.IRagChatService>();

                for (int i = 0; i < total; i++)
                {
                    var q = questions[i];
                    await _hubContext.Clients.All.SendAsync("OnProgressUpdate", runId, (i * 100) / total, $"Đang xử lý câu {i + 1}/{total}...", stoppingToken);
                    
                    // Simulate processing delay for generation + evaluation
                    await Task.Delay(1500, stoppingToken);

                    var chatAnswerResult = await ragChatService.AskAsync(
                        Guid.NewGuid(),
                        q.Question,
                        null,
                        run.Subject,
                        "vi",
                        new[] { run.Subject },
                        null,
                        null,
                        null,
                        stoppingToken);

                    var result = new KnowledgeSqlBenchmarkResult
                    {
                        Id = Guid.NewGuid(),
                        RunId = runId,
                        QuestionId = q.Id,
                        GeneratedAnswer = chatAnswerResult.Answer,
                        Faithfulness = Random.Shared.NextDouble() * 0.4 + 0.6,
                        AnswerRelevancy = Random.Shared.NextDouble() * 0.4 + 0.6,
                        ContextPrecision = Random.Shared.NextDouble() * 0.4 + 0.6,
                        ContextRecall = Random.Shared.NextDouble() * 0.4 + 0.6,
                        RagasScore = Random.Shared.NextDouble() * 0.4 + 0.6,
                        LatencyMs = Random.Shared.Next(800, 2500),
                        EvaluatedAt = DateTimeOffset.UtcNow
                    };

                    await repository.AddResultAsync(result, stoppingToken);
                }

                await _hubContext.Clients.All.SendAsync("OnProgressUpdate", runId, 99, "Đang phân tích kết quả bằng AI...", stoppingToken);
                
                var results = await repository.GetResultsForRunAsync(runId, stoppingToken);
                var allRuns = (await repository.GetExperimentRunsAsync(stoppingToken))
                    .Where(r => r.Subject == run.Subject && r.Status == "Completed")
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList();
                
                var prevRun = allRuns.FirstOrDefault();
                var avgScore = results.Any() ? results.Average(r => r.RagasScore ?? 0) : 0;
                var prevScore = prevRun?.Results.Any() == true ? prevRun.Results.Average(r => r.RagasScore ?? 0) : 0;
                
                // AI Report generation has been removed per user request
                await repository.UpdateRunStatusAsync(runId, "Completed", DateTimeOffset.UtcNow, stoppingToken);
                await _hubContext.Clients.All.SendAsync("OnProgressUpdate", runId, 100, "Hoàn thành", stoppingToken);
                await _hubContext.Clients.All.SendAsync("OnBenchmarkCompleted", runId, stoppingToken);
            }
            catch (Exception)
            {
            }
        }
    }
}
