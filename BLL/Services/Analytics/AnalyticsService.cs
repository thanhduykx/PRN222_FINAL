using PRN222_FINAL.DAL.Repositories.Analytics;
using PRN222_FINAL.BLL.Contracts.Analytics;
using PRN222_FINAL.BLL.Mapping;

namespace PRN222_FINAL.BLL.Services.Analytics;

public sealed class AnalyticsService : IAnalyticsService
{
    private const int MinDays = 1;
    private const int MaxDays = 180;
    private readonly IAnalyticsRepository _analytics;

    public AnalyticsService(IAnalyticsRepository analytics)
    {
        _analytics = analytics;
    }

    public async Task TrackCourseAccessAsync(CourseAccessLogRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.SubjectId == Guid.Empty)
        {
            throw new InvalidOperationException("Subject is required for analytics tracking.");
        }

        if (string.IsNullOrWhiteSpace(request.SubjectCode) && string.IsNullOrWhiteSpace(request.SubjectName))
        {
            throw new InvalidOperationException("Subject code or name is required for analytics tracking.");
        }

        if (request.UserId is null && string.IsNullOrWhiteSpace(request.UserEmail))
        {
            throw new InvalidOperationException("User is required for analytics tracking.");
        }

        request.UserName = Normalize(request.UserName, 255);
        request.UserEmail = Normalize(request.UserEmail, 255);
        request.Role = Normalize(request.Role, 64);
        request.SubjectCode = Normalize(request.SubjectCode, 64);
        request.SubjectName = Normalize(request.SubjectName, 255);
        request.AccessArea = Normalize(string.IsNullOrWhiteSpace(request.AccessArea) ? "course" : request.AccessArea, 64);

        await _analytics.AddCourseAccessAsync(AnalyticsDtoMapper.ToData(request), cancellationToken);
    }

    public async Task<AdminAnalyticsDashboardDto> GetAdminDashboardAsync(int days, CancellationToken cancellationToken = default)
    {
        var normalizedDays = Math.Clamp(days, MinDays, MaxDays);
        var toUtc = DateTimeOffset.UtcNow;
        var fromUtc = toUtc.AddDays(-normalizedDays);
        var data = await _analytics.GetAdminDashboardAsync(fromUtc, toUtc, cancellationToken);
        return AnalyticsDtoMapper.ToDto(data);
    }

    public async Task<AdminAnalyticsDashboardDto> GetAdminDashboardAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        if (toUtc < fromUtc)
        {
            throw new ArgumentException("The analytics end date must be on or after the start date.", nameof(toUtc));
        }

        var data = await _analytics.GetAdminDashboardAsync(fromUtc, toUtc, cancellationToken);
        return AnalyticsDtoMapper.ToDto(data);
    }

    private static string Normalize(string value, int maxLength)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}
