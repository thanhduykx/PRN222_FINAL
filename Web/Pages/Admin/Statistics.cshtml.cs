using PRN222_FINAL.BLL.Services.Accounts;
using PRN222_FINAL.BLL.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.BLL.Services.Analytics;
using PRN222_FINAL.BLL.Services.Billing;
using PRN222_FINAL.BLL.Contracts.Analytics;
using PRN222_FINAL.BLL.Contracts.Billing;
using PRN222_FINAL.Web.Security;
using PRN222_FINAL.Web.Services;
using PRN222_FINAL.Web.ViewModels.Analytics;

namespace PRN222_FINAL.Web.Pages.Admin;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class StatisticsModel : PageModel
{
    private readonly IAnalyticsService _analytics;
    private readonly IUserAccountService _users;
    private readonly IPackageService _packages;
    private readonly IAnalyticsRecommendationService _recommendations;
    private readonly ILogger<StatisticsModel> _logger;
    private AdminAnalyticsDashboardDto? _analyticsSnapshot;

    public StatisticsModel(IAnalyticsService analytics, IUserAccountService users, IPackageService packages,
        IAnalyticsRecommendationService recommendations,
        ILogger<StatisticsModel> logger)
    {
        _analytics = analytics;
        _users = users;
        _packages = packages;
        _recommendations = recommendations;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public int Days { get; set; } = 30;

    [BindProperty(SupportsGet = true)]
    public string Tab { get; set; } = "overview";

    [BindProperty(SupportsGet = true)]
    public string Semester { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public int AcademicYear { get; set; }

    public AdminStatisticsViewModel Dashboard { get; private set; } = new();
    public AdminStatisticsViewModel SemesterDashboard { get; private set; } = new();
    public string SemesterLabel { get; private set; } = string.Empty;
    public IReadOnlyList<int> AcademicYears { get; private set; } = Array.Empty<int>();
    public int TotalUsers { get; private set; }
    public int StudentUsers { get; private set; }
    public int LecturerUsers { get; private set; }
    public int AdminUsers { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;
    public IReadOnlyList<PackageDto> Packages { get; private set; } = Array.Empty<PackageDto>();
    public IReadOnlyList<AnalyticsRecommendationDto> AiRecommendations { get; private set; } = Array.Empty<AnalyticsRecommendationDto>();

    public Task OnGetAsync(CancellationToken cancellationToken) => LoadDashboardAsync(cancellationToken);

    public async Task<IActionResult> OnPostGenerateRecommendationsAsync(CancellationToken cancellationToken)
    {
        await LoadDashboardAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(ErrorMessage))
        {
            try
            {
                AiRecommendations = await _recommendations.GenerateAdminRecommendationsAsync(
                    _analyticsSnapshot ?? throw new InvalidOperationException("Analytics snapshot is unavailable."),
                    "vi",
                    cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                ErrorMessage = "Trợ lý phân tích phản hồi quá chậm. Vui lòng thử lại.";
            }
            catch (Exception)
            {
                ErrorMessage = "Chưa thể tạo đề xuất lúc này. Dữ liệu báo cáo vẫn được giữ nguyên.";
            }
        }

        return Page();
    }

    private async Task LoadDashboardAsync(CancellationToken cancellationToken)
    {
        ViewData["AdminSection"] = "statistics";
        ViewData["ActiveNav"] = "reports";
        Days = Math.Clamp(Days <= 0 ? 30 : Days, 1, 180);
        Tab = NormalizeTab(Tab);
        NormalizeSemesterSelection();
        ViewData["ReportsSection"] = Tab;

        try
        {
            _analyticsSnapshot = await _analytics.GetAdminDashboardAsync(Days, cancellationToken);
            Dashboard = Map(_analyticsSnapshot);
            if (Tab == "chatbot")
            {
                var (semesterFromUtc, semesterToUtc) = GetSemesterRange(AcademicYear, Semester);
                SemesterDashboard = Map(await _analytics.GetAdminDashboardAsync(semesterFromUtc, semesterToUtc, cancellationToken));
            }
            Packages = await _packages.GetActivePackagesAsync(cancellationToken);
            var users = await _users.GetAllAsync(cancellationToken);
            TotalUsers = users.Count;
            StudentUsers = users.Count(user => user.Role == AppRoles.Student);
            LecturerUsers = users.Count(user => user.Role == AppRoles.Lecturer);
            AdminUsers = users.Count(user => user.Role == AppRoles.Admin);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ErrorMessage = "Dữ liệu phản hồi quá chậm. Hãy thử phạm vi ngắn hơn hoặc tải lại trang.";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Admin analytics dashboard could not be loaded for {Days} days.", Days);
            ErrorMessage = "Không thể tải báo cáo lúc này. Vui lòng thử lại sau.";
        }
    }

    private static AdminStatisticsViewModel Map(AdminAnalyticsDashboardDto dto) => new()
    {
        GeneratedAt = dto.GeneratedAt,
        FromUtc = dto.FromUtc,
        ToUtc = dto.ToUtc,
        TotalChatSessions = dto.TotalChatSessions,
        TotalChatQuestions = dto.TotalChatQuestions,
        TotalAssistantAnswers = dto.TotalAssistantAnswers,
        ActiveChatUsers = dto.ActiveChatUsers,
        ReturningChatUsers = dto.ReturningChatUsers,
        TotalSubjects = dto.TotalSubjects,
        TotalDocuments = dto.TotalDocuments,
        IndexedDocuments = dto.IndexedDocuments,
        ProcessingDocuments = dto.ProcessingDocuments,
        FailedDocuments = dto.FailedDocuments,
        PaidRevenueVnd = dto.PaidRevenueVnd,
        PaidPaymentCount = dto.PaidPaymentCount,
        PendingPaymentCount = dto.PendingPaymentCount,
        TotalSubscriptionCount = dto.TotalSubscriptionCount,
        NewSubscriptionCount = dto.NewSubscriptionCount,
        CanceledSubscriptionCount = dto.CanceledSubscriptionCount,
        ActiveSubscriptionCount = dto.ActiveSubscriptionCount,
        SubjectUsage = dto.SubjectUsage.Select(subject => new SubjectUsageViewModel
        {
            SubjectCode = subject.SubjectCode,
            SubjectName = subject.SubjectName,
            OwnerName = subject.OwnerName,
            OwnerEmail = subject.OwnerEmail,
            LecturerCount = subject.LecturerCount,
            StudentCount = subject.StudentCount,
            DocumentCount = subject.DocumentCount,
            IndexedDocumentCount = subject.IndexedDocumentCount,
            ChunkCount = subject.ChunkCount,
            ChatCitationCount = subject.ChatCitationCount,
            CourseAccessCount = subject.CourseAccessCount,
            LastAccessedAt = subject.LastAccessedAt,
            LastChatAt = subject.LastChatAt
        }).ToList(),
        PackagePurchases = dto.PackagePurchases.Select(package => new PackagePurchaseStatsViewModel
        {
            PackageCode = package.PackageCode,
            PackageName = package.PackageName,
            PaidCount = package.PaidCount,
            PendingCount = package.PendingCount,
            FailedCount = package.FailedCount,
            RevenueVnd = package.RevenueVnd,
            ActiveSubscriptionCount = package.ActiveSubscriptionCount
        }).ToList(),
        DailyChatUsage = dto.DailyChatUsage.Select(day => new DailyChatUsageViewModel
        {
            DateLabel = day.Date.ToString("dd/MM"),
            QuestionCount = day.QuestionCount,
            SessionCount = day.SessionCount,
            ActiveUserCount = day.ActiveUserCount
        }).ToList(),
        ChatTimeSlotUsage = dto.ChatTimeSlotUsage.Select(slot => new ChatTimeSlotUsageViewModel
        {
            Label = slot.Label,
            QuestionCount = slot.QuestionCount,
            ActiveUserCount = slot.ActiveUserCount
        }).ToList(),
        DailySubscriptionUsage = dto.DailySubscriptionUsage.Select(day => new DailySubscriptionUsageViewModel
        {
            DateLabel = day.Date.ToString("dd/MM"),
            NewSubscriptionCount = day.NewSubscriptionCount,
            SuccessfulPaymentCount = day.SuccessfulPaymentCount
        }).ToList(),
        TopChatUsers = dto.TopChatUsers.Select(user => new UserChatUsageViewModel
        {
            UserName = string.IsNullOrWhiteSpace(user.UserName) ? user.UserEmail : user.UserName,
            UserEmail = user.UserEmail,
            QuestionCount = user.QuestionCount,
            SessionCount = user.SessionCount,
            LastQuestionAt = user.LastQuestionAt
        }).ToList(),
        RecentDocuments = dto.RecentDocuments.Select(document => new DocumentAnalyticsViewModel
        {
            FileName = document.FileName,
            Subject = document.Subject,
            UploadedByName = document.UploadedByName,
            UploadedByEmail = document.UploadedByEmail,
            Status = document.Status,
            CitationCount = document.CitationCount,
            UploadedAt = document.UploadedAt
        }).ToList(),
        RecentPayments = dto.RecentPayments.Select(payment => new RecentPaymentViewModel
        {
            UserName = string.IsNullOrWhiteSpace(payment.UserName) ? payment.UserEmail : payment.UserName,
            UserEmail = payment.UserEmail,
            PackageName = payment.PackageName,
            Provider = payment.Provider.ToString(),
            Status = payment.Status.ToString(),
            AmountVnd = payment.AmountVnd,
            CreatedAt = payment.CreatedAt,
            PaidAt = payment.PaidAt
        }).ToList()
    };

    private static string NormalizeTab(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "overview" or "chatbot" or "documents" or "users" or "permissions" or "subscription" or "billing"
            ? normalized
            : "overview";
    }

    private void NormalizeSemesterSelection()
    {
        var currentYear = DateTimeOffset.Now.Year;
        AcademicYear = AcademicYear == 0 ? currentYear : Math.Clamp(AcademicYear, currentYear - 4, currentYear);
        AcademicYears = Enumerable.Range(currentYear - 4, 5).Reverse().ToArray();

        var normalized = (Semester ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized is not ("spring" or "summer" or "fall"))
        {
            normalized = DateTimeOffset.Now.Month switch
            {
                <= 4 => "spring",
                <= 8 => "summer",
                _ => "fall"
            };
        }

        Semester = normalized;
        SemesterLabel = normalized switch
        {
            "spring" => $"Spring {AcademicYear} (01/01 - 30/04)",
            "summer" => $"Summer {AcademicYear} (01/05 - 31/08)",
            _ => $"Fall {AcademicYear} (01/09 - 31/12)"
        };
    }

    private static (DateTimeOffset FromUtc, DateTimeOffset ToUtc) GetSemesterRange(int year, string semester)
    {
        var (startMonth, endMonth) = semester switch
        {
            "spring" => (1, 4),
            "summer" => (5, 8),
            _ => (9, 12)
        };
        var offset = TimeZoneInfo.Local.GetUtcOffset(new DateTime(year, startMonth, 1));
        var fromLocal = new DateTimeOffset(year, startMonth, 1, 0, 0, 0, offset);
        var endOffset = TimeZoneInfo.Local.GetUtcOffset(new DateTime(year, endMonth, 1));
        var toLocal = new DateTimeOffset(year, endMonth, 1, 0, 0, 0, endOffset).AddMonths(1).AddTicks(-1);
        return (fromLocal.ToUniversalTime(), toLocal.ToUniversalTime());
    }
}
