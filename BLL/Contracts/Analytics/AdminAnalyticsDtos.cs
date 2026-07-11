using PRN222_FINAL.BLL.Models;

namespace PRN222_FINAL.BLL.Contracts.Analytics;

public sealed class CourseAccessLogRequestDto
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

public sealed class AdminAnalyticsDashboardDto
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
    public IReadOnlyList<SubjectUsageDto> SubjectUsage { get; set; } = Array.Empty<SubjectUsageDto>();
    public IReadOnlyList<PackagePurchaseStatsDto> PackagePurchases { get; set; } = Array.Empty<PackagePurchaseStatsDto>();
    public IReadOnlyList<DailyChatUsageDto> DailyChatUsage { get; set; } = Array.Empty<DailyChatUsageDto>();
    public IReadOnlyList<DailySubscriptionUsageDto> DailySubscriptionUsage { get; set; } = Array.Empty<DailySubscriptionUsageDto>();
    public IReadOnlyList<UserChatUsageDto> TopChatUsers { get; set; } = Array.Empty<UserChatUsageDto>();
    public IReadOnlyList<DocumentAnalyticsDto> RecentDocuments { get; set; } = Array.Empty<DocumentAnalyticsDto>();
    public IReadOnlyList<RecentPaymentDto> RecentPayments { get; set; } = Array.Empty<RecentPaymentDto>();
}

public sealed class SubjectUsageDto
{
    public Guid SubjectId { get; set; }
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

public sealed class PackagePurchaseStatsDto
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

public sealed class DailyChatUsageDto
{
    public DateOnly Date { get; set; }
    public int QuestionCount { get; set; }
    public int SessionCount { get; set; }
    public int ActiveUserCount { get; set; }
}

public sealed class DailySubscriptionUsageDto
{
    public DateOnly Date { get; set; }
    public int NewSubscriptionCount { get; set; }
    public int SuccessfulPaymentCount { get; set; }
}

public sealed class UserChatUsageDto
{
    public Guid? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
    public int SessionCount { get; set; }
    public DateTimeOffset? LastQuestionAt { get; set; }
}

public sealed class DocumentAnalyticsDto
{
    public string FileName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string UploadedByName { get; set; } = string.Empty;
    public string UploadedByEmail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int CitationCount { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}

public sealed class RecentPaymentDto
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
