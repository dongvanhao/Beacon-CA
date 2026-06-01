namespace Beacon.Application.Features.Management.Statistics.Dtos;

public record UserStatisticsDto(int TotalUsers, int ActiveUsers, int InactiveUsers, int OnlineUsers);

public record AdminStatisticsDto(int TotalAdmins, int ActiveAdmins, int InactiveAdmins);

public record PostStatisticsDto(
    int TotalPosts,
    int DeletedPosts,
    int NotDeletedPosts,
    int ActivePosts,
    int HiddenPosts);

public record ReportStatisticsDto(
    int TotalReports,
    int PendingReports,
    int ReviewedReports,
    int ResolvedReports,
    int RejectedReports);

public record DailyPostStatisticDto(DateOnly Date, int TotalPosts);

public record RecentPostStatisticsDto(IReadOnlyList<DailyPostStatisticDto> Items);

public record AdminActivityStatisticItemDto(
    Guid AdminId,
    string? AdminUsername,
    int ActionCount,
    DateTime LastActivityAtUtc);

public record AdminActivityStatisticsDto(
    DateTime FromUtc,
    DateTime ToUtc,
    int ActiveAdminCount,
    int TotalActionCount,
    IReadOnlyList<AdminActivityStatisticItemDto> Items);
