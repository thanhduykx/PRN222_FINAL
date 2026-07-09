using PRN222_FINAL.Models.DTOs.Analytics;

namespace PRN222_FINAL.DAL.Repositories.Analytics;

public interface IAnalyticsRepository
{
    Task AddCourseAccessAsync(CourseAccessLogRequestDto request, CancellationToken cancellationToken = default);
    Task<AdminAnalyticsDashboardDto> GetAdminDashboardAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);
}
