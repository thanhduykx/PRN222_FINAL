using PRN222_FINAL.DAL.Models.Analytics;

namespace PRN222_FINAL.DAL.Repositories.Analytics;

public interface IAnalyticsRepository
{
    Task AddCourseAccessAsync(CourseAccessLogRequestData request, CancellationToken cancellationToken = default);
    Task<AdminAnalyticsDashboardData> GetAdminDashboardAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);
}
