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

    public StatisticsModel(IAnalyticsService analytics, IUserAccountService users, IPackageService packages)
    {
        _analytics = analytics;
        _users = users;
        _packages = packages;
    }

    [BindProperty(SupportsGet = true)]
    public int Days { get; set; } = 30;

    [BindProperty(SupportsGet = true)]
    public string Tab { get; set; } = "overview";

    public AdminStatisticsViewModel Dashboard { get; private set; } = new();
    public int TotalUsers { get; private set; }
    public int StudentUsers { get; private set; }
    public int LecturerUsers { get; private set; }
    public int AdminUsers { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;
    public IReadOnlyList<PackageDto> Packages { get; private set; } = Array.Empty<PackageDto>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ViewData["AdminSection"] = "statistics";
        ViewData["ActiveNav"] = "reports";
        Days = Math.Clamp(Days <= 0 ? 30 : Days, 1, 180);
        Tab = NormalizeTab(Tab);
        ViewData["ReportsSection"] = Tab;

        try
        {
            Dashboard = Map(await _analytics.GetAdminDashboardAsync(Days, cancellationToken));
            Packages = await _packages.GetActivePackagesAsync(cancellationToken);
            var users = await _users.GetAllAsync(cancellationToken);
            TotalUsers = users.Count;
            StudentUsers = users.Count(user => user.Role == AppRoles.Student);
            LecturerUsers = users.Count(user => user.Role == AppRoles.Lecturer);
            AdminUsers = users.Count(user => user.Role == AppRoles.Admin);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
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
}
