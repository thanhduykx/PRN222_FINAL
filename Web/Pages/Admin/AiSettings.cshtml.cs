using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.Web.Security;
using PRN222_FINAL.Web.Services;

namespace PRN222_FINAL.Web.Pages.Admin;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AiSettingsModel : PageModel
{
    private readonly IAiSettingsService _settings;

    public AiSettingsModel(IAiSettingsService settings) => _settings = settings;

    [BindProperty] public InputModel Input { get; set; } = new();

    public void OnGet() => MapFromCurrent();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        try
        {
            await _settings.SaveAsync(new AiSettings(Input.AnswerModel, Input.ReadingModel,
                Input.ReadingDetail, Input.SectionLength, Input.SectionConnection), cancellationToken);
            TempData["Success"] = "Đã lưu thiết lập. Tài liệu mới sẽ dùng cách xử lý này.";
            return RedirectToPage();
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    private void MapFromCurrent()
    {
        var current = _settings.Current;
        Input = new InputModel
        {
            AnswerModel = current.ChatModel,
            ReadingModel = current.EmbeddingModel,
            ReadingDetail = current.EmbeddingDimensions,
            SectionLength = current.ChunkSize,
            SectionConnection = current.ChunkOverlap
        };
    }

    public sealed class InputModel
    {
        [Required, StringLength(120, MinimumLength = 3)] public string AnswerModel { get; set; } = string.Empty;
        [Required, StringLength(120, MinimumLength = 3)] public string ReadingModel { get; set; } = string.Empty;
        [Range(128, 4096)] public int ReadingDetail { get; set; }
        [Range(300, 4000)] public int SectionLength { get; set; }
        [Range(0, 500)] public int SectionConnection { get; set; }
    }
}
