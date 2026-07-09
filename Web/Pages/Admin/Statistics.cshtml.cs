using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.BLL.Services.Analytics;
using PRN222_FINAL.Models.DTOs.Analytics;
using PRN222_FINAL.Web.Security;
using PRN222_FINAL.Web.ViewModels.Analytics;

namespace PRN222_FINAL.Web.Pages.Admin;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class StatisticsModel : PageModel
{
    private readonly IAnalyticsService _analytics;

    public StatisticsModel(IAnalyticsService analytics)
    {
        _analytics = analytics;
    }

    [BindProperty(SupportsGet = true)]
    public int Days { get; set; } = 30;

    public AdminStatisticsViewModel Dashboard { get; private set; } = new();
    public string ErrorMessage { get; private set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ViewData["AdminSection"] = "statistics";
        Days = Math.Clamp(Days <= 0 ? 30 : Days, 1, 180);

        try
        {
            Dashboard = Map(await _analytics.GetAdminDashboardAsync(Days, cancellationToken));
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
        PaidRevenueVnd = dto.PaidRevenueVnd,
        PaidPaymentCount = dto.PaidPaymentCount,
        PendingPaymentCount = dto.PendingPaymentCount,
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
        TopChatUsers = dto.TopChatUsers.Select(user => new UserChatUsageViewModel
        {
            UserName = string.IsNullOrWhiteSpace(user.UserName) ? user.UserEmail : user.UserName,
            UserEmail = user.UserEmail,
            QuestionCount = user.QuestionCount,
            SessionCount = user.SessionCount,
            LastQuestionAt = user.LastQuestionAt
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
}
