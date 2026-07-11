using PRN222_FINAL.BLL.Contracts.Analytics;

namespace PRN222_FINAL.BLL.Services.Analytics;

public interface IAnalyticsService
{
    Task TrackCourseAccessAsync(CourseAccessLogRequestDto request, CancellationToken cancellationToken = default);
    Task<AdminAnalyticsDashboardDto> GetAdminDashboardAsync(int days, CancellationToken cancellationToken = default);
}
