using PRN222_FINAL.BLL.Contracts.Analytics;
using PRN222_FINAL.DAL.Models.Analytics;

namespace PRN222_FINAL.BLL.Mapping;

public static class AnalyticsDtoMapper
{
    public static CourseAccessLogRequestData ToData(CourseAccessLogRequestDto dto) => new()
    {
        UserId = dto.UserId, UserName = dto.UserName, UserEmail = dto.UserEmail,
        Role = dto.Role, SubjectId = dto.SubjectId, SubjectCode = dto.SubjectCode,
        SubjectName = dto.SubjectName, AccessArea = dto.AccessArea
    };

    public static AdminAnalyticsDashboardDto ToDto(AdminAnalyticsDashboardData data) => new()
    {
        GeneratedAt = data.GeneratedAt, FromUtc = data.FromUtc, ToUtc = data.ToUtc,
        TotalChatSessions = data.TotalChatSessions, TotalChatQuestions = data.TotalChatQuestions,
        TotalAssistantAnswers = data.TotalAssistantAnswers, ActiveChatUsers = data.ActiveChatUsers,
        TotalSubjects = data.TotalSubjects, TotalDocuments = data.TotalDocuments,
        IndexedDocuments = data.IndexedDocuments, ProcessingDocuments = data.ProcessingDocuments,
        FailedDocuments = data.FailedDocuments, PaidRevenueVnd = data.PaidRevenueVnd,
        PaidPaymentCount = data.PaidPaymentCount, PendingPaymentCount = data.PendingPaymentCount,
        TotalSubscriptionCount = data.TotalSubscriptionCount,
        NewSubscriptionCount = data.NewSubscriptionCount,
        CanceledSubscriptionCount = data.CanceledSubscriptionCount,
        ActiveSubscriptionCount = data.ActiveSubscriptionCount,
        SubjectUsage = data.SubjectUsage.Select(x => new SubjectUsageDto
        {
            SubjectId=x.SubjectId,SubjectCode=x.SubjectCode,SubjectName=x.SubjectName,
            OwnerName=x.OwnerName,OwnerEmail=x.OwnerEmail,LecturerCount=x.LecturerCount,
            StudentCount=x.StudentCount,DocumentCount=x.DocumentCount,
            IndexedDocumentCount=x.IndexedDocumentCount,ChunkCount=x.ChunkCount,
            ChatCitationCount=x.ChatCitationCount,CourseAccessCount=x.CourseAccessCount,
            LastAccessedAt=x.LastAccessedAt,LastChatAt=x.LastChatAt
        }).ToList(),
        PackagePurchases = data.PackagePurchases.Select(x => new PackagePurchaseStatsDto
        {
            PackageId=x.PackageId,PackageCode=x.PackageCode,PackageName=x.PackageName,
            PaidCount=x.PaidCount,PendingCount=x.PendingCount,FailedCount=x.FailedCount,
            RevenueVnd=x.RevenueVnd,ActiveSubscriptionCount=x.ActiveSubscriptionCount
        }).ToList(),
        DailyChatUsage = data.DailyChatUsage.Select(x => new DailyChatUsageDto
        { Date=x.Date,QuestionCount=x.QuestionCount,SessionCount=x.SessionCount,ActiveUserCount=x.ActiveUserCount }).ToList(),
        DailySubscriptionUsage = data.DailySubscriptionUsage.Select(x => new DailySubscriptionUsageDto
        { Date=x.Date,NewSubscriptionCount=x.NewSubscriptionCount,SuccessfulPaymentCount=x.SuccessfulPaymentCount }).ToList(),
        TopChatUsers = data.TopChatUsers.Select(x => new UserChatUsageDto
        { UserId=x.UserId,UserName=x.UserName,UserEmail=x.UserEmail,QuestionCount=x.QuestionCount,SessionCount=x.SessionCount,LastQuestionAt=x.LastQuestionAt }).ToList(),
        RecentDocuments = data.RecentDocuments.Select(x => new DocumentAnalyticsDto
        { FileName=x.FileName,Subject=x.Subject,UploadedByName=x.UploadedByName,UploadedByEmail=x.UploadedByEmail,Status=x.Status,CitationCount=x.CitationCount,UploadedAt=x.UploadedAt }).ToList(),
        RecentPayments = data.RecentPayments.Select(x => new RecentPaymentDto
        {
            PaymentId=x.PaymentId,UserName=x.UserName,UserEmail=x.UserEmail,PackageName=x.PackageName,
            Provider=(Models.PaymentProvider)x.Provider,Status=(Models.PaymentStatus)x.Status,
            AmountVnd=x.AmountVnd,CreatedAt=x.CreatedAt,PaidAt=x.PaidAt
        }).ToList()
    };
}
