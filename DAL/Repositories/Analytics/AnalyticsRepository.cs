using Microsoft.EntityFrameworkCore;
using PRN222_FINAL.DAL.Entities;
using PRN222_FINAL.DAL.Repositories.Analytics;
using PRN222_FINAL.DAL.Repositories.Billing;
using PRN222_FINAL.DAL.Enums;
using PRN222_FINAL.DAL.Models;
using PRN222_FINAL.DAL.Models.Analytics;

namespace PRN222_FINAL.DAL.Repositories.Analytics;

public sealed class AnalyticsRepository : SqlBillingRepositoryBase, IAnalyticsRepository
{
    public AnalyticsRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task AddCourseAccessAsync(CourseAccessLogRequestData request, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        context.CourseAccessLogs.Add(new KnowledgeSqlCourseAccessLog
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            UserName = Normalize(request.UserName, 255),
            UserEmail = Normalize(request.UserEmail, 255),
            Role = Normalize(request.Role, 64),
            SubjectId = request.SubjectId,
            SubjectCode = Normalize(request.SubjectCode, 64),
            SubjectName = Normalize(request.SubjectName, 255),
            AccessArea = Normalize(request.AccessArea, 64),
            AccessedAt = DateTimeOffset.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<AdminAnalyticsDashboardData> GetAdminDashboardAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();

        var questionRows = await context.Messages
            .AsNoTracking()
            .Where(message => message.Role == "user"
                              && message.CreatedAt >= fromUtc
                              && message.CreatedAt <= toUtc)
            .Join(
                context.Sessions.AsNoTracking(),
                message => message.SessionId,
                session => session.Id,
                (message, session) => new ChatQuestionRow(
                    message.Id,
                    message.SessionId,
                    message.CreatedAt,
                    session.OwnerUserId,
                    session.OwnerName,
                    session.OwnerEmail))
            .ToListAsync(cancellationToken);

        var assistantAnswerCount = await context.Messages
            .AsNoTracking()
            .CountAsync(message => message.Role == "assistant"
                                   && message.CreatedAt >= fromUtc
                                   && message.CreatedAt <= toUtc,
                cancellationToken);

        var totalSessions = await context.Sessions
            .AsNoTracking()
            .CountAsync(session => session.CreatedAt <= toUtc && session.UpdatedAt >= fromUtc, cancellationToken);

        var subjects = await context.CourseSubjects
            .AsNoTracking()
            .Select(subject => new SubjectRow(
                subject.Id,
                subject.Code,
                subject.Name,
                subject.OwnerName ?? string.Empty,
                subject.OwnerEmail ?? string.Empty))
            .ToListAsync(cancellationToken);

        var lecturerCounts = await context.SubjectLecturers
            .AsNoTracking()
            .GroupBy(item => item.SubjectId)
            .Select(group => new CountRow(group.Key, group.Count()))
            .ToDictionaryAsync(item => item.Id, item => item.Count, cancellationToken);

        var studentCounts = await context.SubjectStudents
            .AsNoTracking()
            .GroupBy(item => item.SubjectId)
            .Select(group => new CountRow(group.Key, group.Count()))
            .ToDictionaryAsync(item => item.Id, item => item.Count, cancellationToken);

        var documents = await context.Documents
            .AsNoTracking()
            .Select(document => new DocumentRow(
                document.Id,
                document.FileName,
                document.Subject,
                document.UploadedByName ?? string.Empty,
                document.UploadedByEmail ?? string.Empty,
                document.Status,
                document.ChunkCount,
                document.UploadedAt))
            .ToListAsync(cancellationToken);

        var accessRows = await context.CourseAccessLogs
            .AsNoTracking()
            .Where(log => log.AccessedAt >= fromUtc && log.AccessedAt <= toUtc)
            .Select(log => new AccessRow(log.SubjectId, log.AccessedAt))
            .ToListAsync(cancellationToken);

        var citationRows = await context.Citations
            .AsNoTracking()
            .Where(citation => citation.Message.CreatedAt >= fromUtc && citation.Message.CreatedAt <= toUtc)
            .Select(citation => new CitationRow(citation.DocumentId, citation.Subject, citation.Message.CreatedAt))
            .ToListAsync(cancellationToken);

        var payments = await context.Payments
            .AsNoTracking()
            .Include(payment => payment.Package)
            .Where(payment => payment.CreatedAt >= fromUtc && payment.CreatedAt <= toUtc)
            .Select(payment => new PaymentRow(
                payment.Id,
                payment.PackageId,
                payment.Package == null ? string.Empty : payment.Package.Code,
                payment.Package == null ? string.Empty : payment.Package.Name,
                payment.UserName,
                payment.UserEmail,
                payment.Provider,
                payment.Status,
                payment.AmountVnd,
                payment.CreatedAt,
                payment.PaidAt))
            .ToListAsync(cancellationToken);

        var subscriptions = await context.Subscriptions
            .AsNoTracking()
            .Select(subscription => new SubscriptionRow(
                subscription.Id,
                subscription.PackageId,
                subscription.Status,
                subscription.StartsAt,
                subscription.EndsAt,
                subscription.CreatedAt))
            .ToListAsync(cancellationToken);

        var activeSubscriptionCounts = await context.Subscriptions
            .AsNoTracking()
            .Where(subscription => subscription.Status == SubscriptionStatus.Active
                                   && subscription.StartsAt <= toUtc
                                   && subscription.EndsAt >= fromUtc)
            .GroupBy(subscription => subscription.PackageId)
            .Select(group => new CountRow(group.Key, group.Count()))
            .ToDictionaryAsync(item => item.Id, item => item.Count, cancellationToken);

        var packages = await context.Packages
            .AsNoTracking()
            .OrderBy(package => package.SortOrder)
            .ThenBy(package => package.PriceVnd)
            .Select(package => new PackageRow(package.Id, package.Code, package.Name))
            .ToListAsync(cancellationToken);

        return new AdminAnalyticsDashboardData
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            TotalChatSessions = totalSessions,
            TotalChatQuestions = questionRows.Count,
            TotalAssistantAnswers = assistantAnswerCount,
            ActiveChatUsers = questionRows
                .Where(row => row.OwnerUserId.HasValue)
                .Select(row => row.OwnerUserId!.Value)
                .Distinct()
                .Count(),
            TotalSubjects = subjects.Count,
            TotalDocuments = documents.Count,
            IndexedDocuments = documents.Count(document => document.Status == DocumentIndexStatus.Indexed),
            ProcessingDocuments = documents.Count(document => document.Status == DocumentIndexStatus.Processing),
            FailedDocuments = documents.Count(document => document.Status == DocumentIndexStatus.Failed),
            PaidRevenueVnd = payments.Where(payment => payment.Status == PaymentStatus.Paid).Sum(payment => payment.AmountVnd),
            PaidPaymentCount = payments.Count(payment => payment.Status == PaymentStatus.Paid),
            PendingPaymentCount = payments.Count(payment => payment.Status == PaymentStatus.Pending),
            TotalSubscriptionCount = subscriptions.Count,
            NewSubscriptionCount = subscriptions.Count(subscription => subscription.CreatedAt >= fromUtc && subscription.CreatedAt <= toUtc),
            CanceledSubscriptionCount = subscriptions.Count(subscription => subscription.Status == SubscriptionStatus.Canceled),
            ActiveSubscriptionCount = activeSubscriptionCounts.Values.Sum(),
            SubjectUsage = BuildSubjectUsage(subjects, documents, citationRows, accessRows, lecturerCounts, studentCounts),
            PackagePurchases = BuildPackagePurchases(packages, payments, activeSubscriptionCounts),
            DailyChatUsage = BuildDailyChatUsage(questionRows),
            DailySubscriptionUsage = BuildDailySubscriptionUsage(subscriptions, payments, fromUtc, toUtc),
            TopChatUsers = BuildTopChatUsers(questionRows),
            RecentDocuments = BuildRecentDocuments(documents, citationRows),
            RecentPayments = payments
                .OrderByDescending(payment => payment.CreatedAt)
                .Take(10)
                .Select(payment => new RecentPaymentData
                {
                    PaymentId = payment.Id,
                    UserName = payment.UserName,
                    UserEmail = payment.UserEmail,
                    PackageName = string.IsNullOrWhiteSpace(payment.PackageName) ? payment.PackageCode : payment.PackageName,
                    Provider = payment.Provider,
                    Status = payment.Status,
                    AmountVnd = payment.AmountVnd,
                    CreatedAt = payment.CreatedAt,
                    PaidAt = payment.PaidAt
                })
                .ToList()
        };
    }

    private static IReadOnlyList<SubjectUsageData> BuildSubjectUsage(
        IReadOnlyList<SubjectRow> subjects,
        IReadOnlyList<DocumentRow> documents,
        IReadOnlyList<CitationRow> citations,
        IReadOnlyList<AccessRow> accessRows,
        IReadOnlyDictionary<Guid, int> lecturerCounts,
        IReadOnlyDictionary<Guid, int> studentCounts)
    {
        return subjects
            .Select(subject =>
            {
                var subjectDocuments = documents
                    .Where(document => MatchesSubject(document.Subject, subject.Code, subject.Name))
                    .ToList();
                var subjectCitations = citations
                    .Where(citation => MatchesSubject(citation.Subject, subject.Code, subject.Name))
                    .ToList();
                var subjectAccessRows = accessRows
                    .Where(access => access.SubjectId == subject.Id)
                    .ToList();

                return new SubjectUsageData
                {
                    SubjectId = subject.Id,
                    SubjectCode = subject.Code,
                    SubjectName = subject.Name,
                    OwnerName = subject.OwnerName,
                    OwnerEmail = subject.OwnerEmail,
                    LecturerCount = lecturerCounts.TryGetValue(subject.Id, out var lecturerCount) ? lecturerCount : 0,
                    StudentCount = studentCounts.TryGetValue(subject.Id, out var studentCount) ? studentCount : 0,
                    DocumentCount = subjectDocuments.Count,
                    IndexedDocumentCount = subjectDocuments.Count(document => document.Status == DocumentIndexStatus.Indexed),
                    ChunkCount = subjectDocuments.Sum(document => document.ChunkCount),
                    ChatCitationCount = subjectCitations.Count,
                    CourseAccessCount = subjectAccessRows.Count,
                    LastAccessedAt = subjectAccessRows.Count == 0 ? null : subjectAccessRows.Max(access => access.AccessedAt),
                    LastChatAt = subjectCitations.Count == 0 ? null : subjectCitations.Max(citation => citation.CreatedAt)
                };
            })
            .OrderByDescending(subject => subject.CourseAccessCount)
            .ThenByDescending(subject => subject.ChatCitationCount)
            .ThenBy(subject => subject.SubjectCode)
            .ToList();
    }

    private static IReadOnlyList<PackagePurchaseStatsData> BuildPackagePurchases(
        IReadOnlyList<PackageRow> packages,
        IReadOnlyList<PaymentRow> payments,
        IReadOnlyDictionary<Guid, int> activeSubscriptionCounts)
    {
        return packages
            .Select(package =>
            {
                var packagePayments = payments.Where(payment => payment.PackageId == package.Id).ToList();
                return new PackagePurchaseStatsData
                {
                    PackageId = package.Id,
                    PackageCode = package.Code,
                    PackageName = package.Name,
                    PaidCount = packagePayments.Count(payment => payment.Status == PaymentStatus.Paid),
                    PendingCount = packagePayments.Count(payment => payment.Status == PaymentStatus.Pending),
                    FailedCount = packagePayments.Count(payment => payment.Status is PaymentStatus.Failed or PaymentStatus.Canceled),
                    RevenueVnd = packagePayments.Where(payment => payment.Status == PaymentStatus.Paid).Sum(payment => payment.AmountVnd),
                    ActiveSubscriptionCount = activeSubscriptionCounts.TryGetValue(package.Id, out var count) ? count : 0
                };
            })
            .OrderByDescending(package => package.RevenueVnd)
            .ThenBy(package => package.PackageCode)
            .ToList();
    }

    private static IReadOnlyList<DailyChatUsageData> BuildDailyChatUsage(IReadOnlyList<ChatQuestionRow> questionRows)
    {
        return questionRows
            .GroupBy(row => DateOnly.FromDateTime(row.CreatedAt.LocalDateTime.Date))
            .OrderBy(group => group.Key)
            .Select(group => new DailyChatUsageData
            {
                Date = group.Key,
                QuestionCount = group.Count(),
                SessionCount = group.Select(row => row.SessionId).Distinct().Count(),
                ActiveUserCount = group.Where(row => row.OwnerUserId.HasValue).Select(row => row.OwnerUserId!.Value).Distinct().Count()
            })
            .ToList();
    }

    private static IReadOnlyList<UserChatUsageData> BuildTopChatUsers(IReadOnlyList<ChatQuestionRow> questionRows)
    {
        return questionRows
            .GroupBy(row => row.OwnerUserId)
            .Select(group =>
            {
                var first = group.OrderByDescending(row => row.CreatedAt).First();
                return new UserChatUsageData
                {
                    UserId = group.Key,
                    UserName = first.OwnerName,
                    UserEmail = first.OwnerEmail,
                    QuestionCount = group.Count(),
                    SessionCount = group.Select(row => row.SessionId).Distinct().Count(),
                    LastQuestionAt = group.Max(row => row.CreatedAt)
                };
            })
            .OrderByDescending(user => user.QuestionCount)
            .ThenBy(user => user.UserEmail)
            .Take(10)
            .ToList();
    }

    private static IReadOnlyList<DailySubscriptionUsageData> BuildDailySubscriptionUsage(
        IReadOnlyList<SubscriptionRow> subscriptions,
        IReadOnlyList<PaymentRow> payments,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc)
    {
        var startDate = DateOnly.FromDateTime(fromUtc.LocalDateTime.Date);
        var endDate = DateOnly.FromDateTime(toUtc.LocalDateTime.Date);
        var results = new List<DailySubscriptionUsageData>();

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            results.Add(new DailySubscriptionUsageData
            {
                Date = date,
                NewSubscriptionCount = subscriptions.Count(subscription =>
                    DateOnly.FromDateTime(subscription.CreatedAt.LocalDateTime.Date) == date),
                SuccessfulPaymentCount = payments.Count(payment =>
                    payment.Status == PaymentStatus.Paid
                    && DateOnly.FromDateTime((payment.PaidAt ?? payment.CreatedAt).LocalDateTime.Date) == date)
            });
        }

        return results;
    }

    private static IReadOnlyList<DocumentAnalyticsData> BuildRecentDocuments(
        IReadOnlyList<DocumentRow> documents,
        IReadOnlyList<CitationRow> citations)
    {
        return documents
            .OrderByDescending(document => document.UploadedAt)
            .Take(10)
            .Select(document => new DocumentAnalyticsData
            {
                FileName = document.FileName,
                Subject = document.Subject,
                UploadedByName = document.UploadedByName,
                UploadedByEmail = document.UploadedByEmail,
                Status = document.Status,
                CitationCount = citations.Count(citation => citation.DocumentId == document.Id),
                UploadedAt = document.UploadedAt
            })
            .ToList();
    }

    private static bool MatchesSubject(string value, string subjectCode, string subjectName)
    {
        var normalizedValue = NormalizeCode(value);
        return !string.IsNullOrWhiteSpace(normalizedValue)
               && (normalizedValue == NormalizeCode(subjectCode)
                   || normalizedValue == NormalizeCode(subjectName)
                   || normalizedValue.StartsWith($"{NormalizeCode(subjectCode)}-", StringComparison.Ordinal));
    }

    private static string NormalizeCode(string value)
    {
        var trimmed = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (trimmed.Contains(" - ", StringComparison.Ordinal))
        {
            trimmed = trimmed[..trimmed.IndexOf(" - ", StringComparison.Ordinal)];
        }

        return new string(trimmed.Where(character => char.IsLetterOrDigit(character) || character is '_' or '.' or '-').ToArray());
    }

    private static string Normalize(string value, int maxLength)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private sealed record ChatQuestionRow(Guid Id, Guid SessionId, DateTimeOffset CreatedAt, Guid? OwnerUserId, string OwnerName, string OwnerEmail);
    private sealed record SubjectRow(Guid Id, string Code, string Name, string OwnerName, string OwnerEmail);
    private sealed record DocumentRow(
        Guid Id,
        string FileName,
        string Subject,
        string UploadedByName,
        string UploadedByEmail,
        string Status,
        int ChunkCount,
        DateTimeOffset UploadedAt);
    private sealed record CitationRow(Guid DocumentId, string Subject, DateTimeOffset CreatedAt);
    private sealed record AccessRow(Guid SubjectId, DateTimeOffset AccessedAt);
    private sealed record PackageRow(Guid Id, string Code, string Name);
    private sealed record CountRow(Guid Id, int Count);
    private sealed record SubscriptionRow(
        Guid Id,
        Guid PackageId,
        SubscriptionStatus Status,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt,
        DateTimeOffset CreatedAt);
    private sealed record PaymentRow(
        Guid Id,
        Guid PackageId,
        string PackageCode,
        string PackageName,
        string UserName,
        string UserEmail,
        PaymentProvider Provider,
        PaymentStatus Status,
        decimal AmountVnd,
        DateTimeOffset CreatedAt,
        DateTimeOffset? PaidAt);
}
