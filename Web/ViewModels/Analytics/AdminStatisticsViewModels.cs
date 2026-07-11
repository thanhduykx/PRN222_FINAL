namespace PRN222_FINAL.Web.ViewModels.Analytics;

public sealed class AdminStatisticsViewModel
{
    public DateTimeOffset GeneratedAt { get; set; }
    public DateTimeOffset FromUtc { get; set; }
    public DateTimeOffset ToUtc { get; set; }
    public int TotalChatSessions { get; set; }
    public int TotalChatQuestions { get; set; }
    public int TotalAssistantAnswers { get; set; }
    public int ActiveChatUsers { get; set; }
    public int TotalSubjects { get; set; }
    public int TotalDocuments { get; set; }
    public int IndexedDocuments { get; set; }
    public int ProcessingDocuments { get; set; }
    public int FailedDocuments { get; set; }
    public decimal PaidRevenueVnd { get; set; }
    public int PaidPaymentCount { get; set; }
    public int PendingPaymentCount { get; set; }
    public int TotalSubscriptionCount { get; set; }
    public int NewSubscriptionCount { get; set; }
    public int CanceledSubscriptionCount { get; set; }
    public int ActiveSubscriptionCount { get; set; }
    public IReadOnlyList<SubjectUsageViewModel> SubjectUsage { get; set; } = Array.Empty<SubjectUsageViewModel>();
    public IReadOnlyList<PackagePurchaseStatsViewModel> PackagePurchases { get; set; } = Array.Empty<PackagePurchaseStatsViewModel>();
    public IReadOnlyList<DailyChatUsageViewModel> DailyChatUsage { get; set; } = Array.Empty<DailyChatUsageViewModel>();
    public IReadOnlyList<DailySubscriptionUsageViewModel> DailySubscriptionUsage { get; set; } = Array.Empty<DailySubscriptionUsageViewModel>();
    public IReadOnlyList<UserChatUsageViewModel> TopChatUsers { get; set; } = Array.Empty<UserChatUsageViewModel>();
    public IReadOnlyList<DocumentAnalyticsViewModel> RecentDocuments { get; set; } = Array.Empty<DocumentAnalyticsViewModel>();
    public IReadOnlyList<RecentPaymentViewModel> RecentPayments { get; set; } = Array.Empty<RecentPaymentViewModel>();
}

public sealed class SubjectUsageViewModel
{
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
    public int LecturerCount { get; set; }
    public int StudentCount { get; set; }
    public int DocumentCount { get; set; }
    public int IndexedDocumentCount { get; set; }
    public int ChunkCount { get; set; }
    public int ChatCitationCount { get; set; }
    public int CourseAccessCount { get; set; }
    public DateTimeOffset? LastAccessedAt { get; set; }
    public DateTimeOffset? LastChatAt { get; set; }
}

public sealed class PackagePurchaseStatsViewModel
{
    public string PackageCode { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public int PaidCount { get; set; }
    public int PendingCount { get; set; }
    public int FailedCount { get; set; }
    public decimal RevenueVnd { get; set; }
    public int ActiveSubscriptionCount { get; set; }
}

public sealed class DailyChatUsageViewModel
{
    public string DateLabel { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
    public int SessionCount { get; set; }
    public int ActiveUserCount { get; set; }
}

public sealed class DailySubscriptionUsageViewModel
{
    public string DateLabel { get; set; } = string.Empty;
    public int NewSubscriptionCount { get; set; }
    public int SuccessfulPaymentCount { get; set; }
}

public sealed class UserChatUsageViewModel
{
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
    public int SessionCount { get; set; }
    public DateTimeOffset? LastQuestionAt { get; set; }
}

public sealed class DocumentAnalyticsViewModel
{
    public string FileName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string UploadedByName { get; set; } = string.Empty;
    public string UploadedByEmail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int CitationCount { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}

public sealed class RecentPaymentViewModel
{
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal AmountVnd { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
}
