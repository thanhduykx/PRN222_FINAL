using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.Web.Security;
using PRN222_FINAL.BLL;
using PRN222_FINAL.BLL.Services;

namespace PRN222_FINAL.Web.Pages.Admin;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AiSettingsModel : PageModel
{
    private readonly IAiSettingsService _settings;

    public AiSettingsModel(IAiSettingsService settings) => _settings = settings;

    [BindProperty] public InputModel Input { get; set; } = new();
    public IReadOnlyList<string> AnswerProviderOptions => _settings.SupportedChatProviders;
    public IReadOnlyList<string> AnswerModelOptions => _settings.SupportedChatModels;
    public IReadOnlyDictionary<string, IReadOnlyList<string>> AnswerModelsByProvider => _settings.SupportedChatModelsByProvider;
    public IReadOnlyList<string> ReadingModelOptions => _settings.SupportedEmbeddingModels;
    public bool IsSelectedProviderConfigured => _settings.IsChatProviderConfigured(Input.AnswerProvider);
    public bool IsProviderConfigured(string provider) => _settings.IsChatProviderConfigured(provider);

    public void OnGet() => MapFromCurrent();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var providerVal = ModelState["Input.AnswerProvider"]?.AttemptedValue ?? "null";
            var modelVal = ModelState["Input.AnswerModel"]?.AttemptedValue ?? "null";
            Console.WriteLine($"[AiSettings] Validation Failed! AnswerProvider='{providerVal}', AnswerModel='{modelVal}'");
            return Page();
        }
        try
        {
            if (!_settings.IsChatProviderConfigured(Input.AnswerProvider))
            {
                ModelState.AddModelError("Input.AnswerProvider", "Nhà cung cấp chưa có API key hợp lệ trong cấu hình máy chủ.");
                return Page();
            }

            await _settings.SaveAsync(new AiSettings(Input.AnswerModel, Input.ReadingModel,
                Input.ReadingDetail, Input.SectionLength, Input.SectionConnection, Input.AnswerProvider), cancellationToken);
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
            AnswerProvider = current.ChatProvider,
            AnswerModel = current.ChatModel,
            ReadingModel = current.EmbeddingModel,
            ReadingDetail = current.EmbeddingDimensions,
            SectionLength = current.ChunkSize,
            SectionConnection = current.ChunkOverlap
        };
    }

    public sealed class InputModel
    {
        [Required, StringLength(32)] public string AnswerProvider { get; set; } = ChatProviders.Gemini;
        [Required, StringLength(120)] public string AnswerModel { get; set; } = string.Empty;
        [Required, StringLength(120)] public string ReadingModel { get; set; } = string.Empty;
        [Range(128, 4096)] public int ReadingDetail { get; set; }
        [Range(300, 4000)] public int SectionLength { get; set; }
        [Range(0, 500)] public int SectionConnection { get; set; }
    }
}
