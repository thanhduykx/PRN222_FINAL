using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PRN222_FINAL.BLL.Contracts.Analytics;

namespace PRN222_FINAL.BLL.Services.Analytics;

public sealed record AnalyticsRecommendationDto(
    string Priority,
    string Title,
    string Reason,
    string Action,
    bool GeneratedByAi);

public interface IAnalyticsRecommendationService
{
    Task<IReadOnlyList<AnalyticsRecommendationDto>> GenerateAdminRecommendationsAsync(
        AdminAnalyticsDashboardDto dashboard,
        string language,
        CancellationToken cancellationToken = default);
}

public sealed class AnalyticsRecommendationService : IAnalyticsRecommendationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ILocalChatCompletionService _chat;

    public AnalyticsRecommendationService(ILocalChatCompletionService chat)
    {
        _chat = chat;
    }

    public async Task<IReadOnlyList<AnalyticsRecommendationDto>> GenerateAdminRecommendationsAsync(
        AdminAnalyticsDashboardDto dashboard,
        string language,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dashboard);
        var fallback = BuildRuleBasedRecommendations(dashboard);
        if (!_chat.IsEnabled)
        {
            return fallback;
        }

        var response = await _chat.GenerateAnalyticsRecommendationsAsync(
            BuildSummary(dashboard),
            language,
            cancellationToken);
        var generated = ParseRecommendations(response);
        return generated.Count == 0 ? fallback : generated;
    }

    private static string BuildSummary(AdminAnalyticsDashboardDto dashboard)
    {
        var unanswered = Math.Max(0, dashboard.TotalChatQuestions - dashboard.TotalAssistantAnswers);
        var subjectsWithoutDocuments = dashboard.SubjectUsage.Count(subject => subject.DocumentCount == 0);
        var subjectsWithoutOwner = dashboard.SubjectUsage.Count(subject => string.IsNullOrWhiteSpace(subject.OwnerEmail));
        var builder = new StringBuilder()
            .AppendLine($"Period: {dashboard.FromUtc:yyyy-MM-dd} to {dashboard.ToUtc:yyyy-MM-dd}")
            .AppendLine($"Chat questions: {dashboard.TotalChatQuestions}")
            .AppendLine($"Assistant answers: {dashboard.TotalAssistantAnswers}")
            .AppendLine($"Unanswered questions: {unanswered}")
            .AppendLine($"Active chat users: {dashboard.ActiveChatUsers}")
            .AppendLine($"Documents total/indexed/processing/failed: {dashboard.TotalDocuments}/{dashboard.IndexedDocuments}/{dashboard.ProcessingDocuments}/{dashboard.FailedDocuments}")
            .AppendLine($"Subjects total/without documents/without owner: {dashboard.TotalSubjects}/{subjectsWithoutDocuments}/{subjectsWithoutOwner}")
            .AppendLine($"Subscriptions active/new/canceled: {dashboard.ActiveSubscriptionCount}/{dashboard.NewSubscriptionCount}/{dashboard.CanceledSubscriptionCount}")
            .AppendLine($"Payments paid/pending/revenue VND: {dashboard.PaidPaymentCount}/{dashboard.PendingPaymentCount}/{dashboard.PaidRevenueVnd:0}");

        foreach (var subject in dashboard.SubjectUsage
                     .OrderByDescending(item => item.ChatCitationCount)
                     .ThenByDescending(item => item.CourseAccessCount)
                     .Take(5))
        {
            builder.AppendLine($"Subject {subject.SubjectCode}: documents={subject.DocumentCount}, indexed={subject.IndexedDocumentCount}, citations={subject.ChatCitationCount}, accesses={subject.CourseAccessCount}");
        }

        return builder.ToString();
    }

    private static IReadOnlyList<AnalyticsRecommendationDto> BuildRuleBasedRecommendations(AdminAnalyticsDashboardDto dashboard)
    {
        var items = new List<AnalyticsRecommendationDto>();
        var unanswered = Math.Max(0, dashboard.TotalChatQuestions - dashboard.TotalAssistantAnswers);
        if (dashboard.FailedDocuments > 0)
        {
            items.Add(new("high", "Xử lý tài liệu lỗi", $"Có {dashboard.FailedDocuments:N0} tài liệu xử lý thất bại.", "Mở Kho tài liệu, lọc trạng thái lỗi và kiểm tra lại file trước khi chạy lại.", false));
        }
        if (unanswered > 0)
        {
            items.Add(new("high", "Bổ sung nguồn cho câu hỏi chưa trả lời", $"Có {unanswered:N0} câu hỏi chưa nhận được câu trả lời.", "Đối chiếu các môn được hỏi nhiều với độ phủ tài liệu và bổ sung học liệu còn thiếu.", false));
        }

        var missingDocuments = dashboard.SubjectUsage.Count(subject => subject.DocumentCount == 0);
        if (missingDocuments > 0)
        {
            items.Add(new("medium", "Hoàn thiện kho học liệu", $"Có {missingDocuments:N0} môn chưa có tài liệu.", "Phân công giảng viên phụ trách và đặt hạn tải tài liệu cho các môn này.", false));
        }

        if (dashboard.PendingPaymentCount > 0)
        {
            items.Add(new("medium", "Theo dõi thanh toán chờ xử lý", $"Có {dashboard.PendingPaymentCount:N0} giao dịch đang chờ.", "Kiểm tra trạng thái webhook và đối soát các giao dịch chưa hoàn tất.", false));
        }

        if (items.Count == 0)
        {
            items.Add(new("low", "Hệ thống đang ổn định", "Không có cảnh báo vận hành nổi bật trong phạm vi đã chọn.", "Tiếp tục theo dõi mức sử dụng và độ phủ tài liệu theo tuần.", false));
        }

        return items.Take(4).ToList();
    }

    private static IReadOnlyList<AnalyticsRecommendationDto> ParseRecommendations(string? response)
    {
        if (string.IsNullOrWhiteSpace(response)) return Array.Empty<AnalyticsRecommendationDto>();
        var start = response.IndexOf('{');
        var end = response.LastIndexOf('}');
        if (start < 0 || end <= start) return Array.Empty<AnalyticsRecommendationDto>();

        try
        {
            var payload = JsonSerializer.Deserialize<RecommendationResponse>(response[start..(end + 1)], JsonOptions);
            var recommendations = payload?.Recommendations?
                .Where(item => !string.IsNullOrWhiteSpace(item.Title) && !string.IsNullOrWhiteSpace(item.Action))
                .Take(4)
                .Select(item => new AnalyticsRecommendationDto(
                    NormalizePriority(item.Priority),
                    item.Title.Trim(),
                    item.Reason?.Trim() ?? string.Empty,
                    item.Action.Trim(),
                    true))
                .ToList();
            return recommendations ?? new List<AnalyticsRecommendationDto>();
        }
        catch (JsonException)
        {
            return Array.Empty<AnalyticsRecommendationDto>();
        }
    }

    private static string NormalizePriority(string? priority) => priority?.Trim().ToLowerInvariant() switch
    {
        "high" => "high",
        "low" => "low",
        _ => "medium"
    };

    private sealed record RecommendationResponse(
        [property: JsonPropertyName("recommendations")] IReadOnlyList<RecommendationItem>? Recommendations);

    private sealed record RecommendationItem(
        [property: JsonPropertyName("priority")] string? Priority,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("reason")] string? Reason,
        [property: JsonPropertyName("action")] string Action);
}
