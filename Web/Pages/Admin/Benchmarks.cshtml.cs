using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.BLL.Contracts.Benchmarks;
using PRN222_FINAL.BLL.Services.Benchmarks;
using PRN222_FINAL.Web.Security;

namespace PRN222_FINAL.Web.Pages.Admin;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class BenchmarksModel : PageModel
{
    private readonly IBenchmarkService _benchmarkService;
    private readonly PRN222_FINAL.BLL.IKnowledgeService _knowledgeService;

    public BenchmarksModel(IBenchmarkService benchmarkService, PRN222_FINAL.BLL.IKnowledgeService knowledgeService)
    {
        _benchmarkService = benchmarkService;
        _knowledgeService = knowledgeService;
    }

    public IReadOnlyList<ExperimentRunDto> Runs { get; set; } = Array.Empty<ExperimentRunDto>();
    public IReadOnlyList<string> IndexedSubjects { get; set; } = Array.Empty<string>();

    public async Task OnGetAsync()
    {
        Runs = await _benchmarkService.GetExperimentRunsAsync();
        var scope = new PRN222_FINAL.BLL.Models.DocumentAccessScope("Admin", null, null, PRN222_FINAL.BLL.Models.DocumentAccessMode.DocumentUi);
        IndexedSubjects = await _knowledgeService.GetIndexedSubjectsAsync(scope);
    }

    public async Task<IActionResult> OnPostRunBenchmarkAsync(string subject, string chatModel, string embeddingModel)
    {
        if (string.IsNullOrWhiteSpace(subject)) return new JsonResult(new { success = false, message = "Thiếu môn học" });
        
        await _benchmarkService.SeedMockQuestionsAsync(subject);
        var runId = await _benchmarkService.StartBenchmarkAsync(subject, chatModel ?? "gemini-3.5-flash", embeddingModel ?? "gemini-embedding-2");
        
        return new JsonResult(new { success = true, runId });
    }

    public async Task<IActionResult> OnGetRunDetailsAsync(Guid runId)
    {
        var run = await _benchmarkService.GetExperimentRunAsync(runId);
        if (run == null) return NotFound();
        
        var results = await _benchmarkService.GetResultsForRunAsync(runId);
        return new JsonResult(new { success = true, run = run, results = results });
    }
}
