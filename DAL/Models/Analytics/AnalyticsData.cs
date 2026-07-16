using PRN222_FINAL.DAL.Enums;

namespace PRN222_FINAL.DAL.Models.Analytics;

public sealed class CourseAccessLogRequestData
{
    public Guid? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string AccessArea { get; set; } = string.Empty;
}

public sealed class AdminAnalyticsDashboardData
{
    public DateTimeOffset GeneratedAt { get; set; }
    public DateTimeOffset FromUtc { get; set; }
    public DateTimeOffset ToUtc { get; set; }
    public int TotalChatSessions { get; set; }
    public int TotalChatQuestions { get; set; }
    public int TotalAssistantAnswers { get; set; }
    public int ActiveChatUsers { get; set; }
    public int ReturningChatUsers { get; set; }
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
    public IReadOnlyList<SubjectUsageData> SubjectUsage { get; set; } = Array.Empty<SubjectUsageData>();
    public IReadOnlyList<PackagePurchaseStatsData> PackagePurchases { get; set; } = Array.Empty<PackagePurchaseStatsData>();
    public IReadOnlyList<DailyChatUsageData> DailyChatUsage { get; set; } = Array.Empty<DailyChatUsageData>();
    public IReadOnlyList<ChatTimeSlotUsageData> ChatTimeSlotUsage { get; set; } = Array.Empty<ChatTimeSlotUsageData>();
    public IReadOnlyList<DailySubscriptionUsageData> DailySubscriptionUsage { get; set; } = Array.Empty<DailySubscriptionUsageData>();
    public IReadOnlyList<UserChatUsageData> TopChatUsers { get; set; } = Array.Empty<UserChatUsageData>();
    public IReadOnlyList<DocumentAnalyticsData> RecentDocuments { get; set; } = Array.Empty<DocumentAnalyticsData>();
    public IReadOnlyList<RecentPaymentData> RecentPayments { get; set; } = Array.Empty<RecentPaymentData>();
}

public sealed class SubjectUsageData
{
    public Guid SubjectId { get; set; }
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public Guid? OwnerUserId { get; set; }
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

public sealed class PackagePurchaseStatsData
{
    public Guid PackageId { get; set; }
    public string PackageCode { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public int PaidCount { get; set; }
    public int PendingCount { get; set; }
    public int FailedCount { get; set; }
    public decimal RevenueVnd { get; set; }
    public int ActiveSubscriptionCount { get; set; }
}

public sealed class DailyChatUsageData
{
    public DateOnly Date { get; set; }
    public int QuestionCount { get; set; }
    public int SessionCount { get; set; }
    public int ActiveUserCount { get; set; }
}

public sealed class ChatTimeSlotUsageData
{
    public string Label { get; set; } = string.Empty;
    public int StartHour { get; set; }
    public int EndHour { get; set; }
    public int QuestionCount { get; set; }
    public int ActiveUserCount { get; set; }
}

public sealed class DailySubscriptionUsageData
{
    public DateOnly Date { get; set; }
    public int NewSubscriptionCount { get; set; }
    public int SuccessfulPaymentCount { get; set; }
}

public sealed class UserChatUsageData
{
    public Guid? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
    public int SessionCount { get; set; }
    public DateTimeOffset? LastQuestionAt { get; set; }
}

public sealed class DocumentAnalyticsData
{
    public string FileName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string UploadedByName { get; set; } = string.Empty;
    public string UploadedByEmail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int CitationCount { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}

public sealed class RecentPaymentData
{
    public Guid PaymentId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public PaymentProvider Provider { get; set; }
    public PaymentStatus Status { get; set; }
    public decimal AmountVnd { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
}
