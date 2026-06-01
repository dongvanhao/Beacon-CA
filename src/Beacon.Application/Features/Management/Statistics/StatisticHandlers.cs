using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Management.Statistics.Dtos;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Posts;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Management.Statistics;

public record GetUserStatisticsQuery : IRequest<Result<UserStatisticsDto>>;
public record GetAdminStatisticsQuery : IRequest<Result<AdminStatisticsDto>>;
public record GetPostStatisticsQuery : IRequest<Result<PostStatisticsDto>>;
public record GetReportStatisticsQuery : IRequest<Result<ReportStatisticsDto>>;
public record GetRecentPostStatisticsQuery : IRequest<Result<RecentPostStatisticsDto>>;
public record GetAdminActivityStatisticsQuery(DateTime? FromUtc, DateTime? ToUtc)
    : IRequest<Result<AdminActivityStatisticsDto>>;

public class GetUserStatisticsQueryHandler(
    IUserRepository userRepo,
    IUserOnlineTracker onlineTracker)
    : IRequestHandler<GetUserStatisticsQuery, Result<UserStatisticsDto>>
{
    public async Task<Result<UserStatisticsDto>> Handle(GetUserStatisticsQuery query, CancellationToken ct)
    {
        var counts = await userRepo.CountStatusAsync(ct);
        return Result<UserStatisticsDto>.Success(new UserStatisticsDto(
            counts.Total,
            counts.Active,
            counts.Inactive,
            onlineTracker.OnlineUserCount));
    }
}

public class GetAdminStatisticsQueryHandler(IAdminRepository adminRepo)
    : IRequestHandler<GetAdminStatisticsQuery, Result<AdminStatisticsDto>>
{
    public async Task<Result<AdminStatisticsDto>> Handle(GetAdminStatisticsQuery query, CancellationToken ct)
    {
        var counts = await adminRepo.CountStatusAsync(ct);
        return Result<AdminStatisticsDto>.Success(new AdminStatisticsDto(counts.Total, counts.Active, counts.Inactive));
    }
}

public class GetPostStatisticsQueryHandler(IPostRepository postRepo)
    : IRequestHandler<GetPostStatisticsQuery, Result<PostStatisticsDto>>
{
    public async Task<Result<PostStatisticsDto>> Handle(GetPostStatisticsQuery query, CancellationToken ct)
    {
        var counts = await postRepo.CountStatusForManagementAsync(ct);
        return Result<PostStatisticsDto>.Success(new PostStatisticsDto(
            counts.Total,
            counts.Deleted,
            counts.NotDeleted,
            counts.Active,
            counts.Hidden));
    }
}

public class GetReportStatisticsQueryHandler(IPostReportRepository reportRepo)
    : IRequestHandler<GetReportStatisticsQuery, Result<ReportStatisticsDto>>
{
    public async Task<Result<ReportStatisticsDto>> Handle(GetReportStatisticsQuery query, CancellationToken ct)
    {
        var counts = await reportRepo.CountStatusAsync(ct);
        return Result<ReportStatisticsDto>.Success(new ReportStatisticsDto(
            counts.Total,
            counts.Pending,
            counts.Reviewed,
            counts.Resolved,
            counts.Rejected));
    }
}

public class GetRecentPostStatisticsQueryHandler(IPostRepository postRepo)
    : IRequestHandler<GetRecentPostStatisticsQuery, Result<RecentPostStatisticsDto>>
{
    public async Task<Result<RecentPostStatisticsDto>> Handle(GetRecentPostStatisticsQuery query, CancellationToken ct)
    {
        var toDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = toDate.AddDays(-9);
        var counts = await postRepo.CountCreatedByDateAsync(fromDate, toDate, ct);

        return Result<RecentPostStatisticsDto>.Success(new RecentPostStatisticsDto(
            counts.Select(x => new DailyPostStatisticDto(x.Date, x.Count)).ToList()));
    }
}

public class GetAdminActivityStatisticsQueryHandler(IAdminAuditLogService auditLogService)
    : IRequestHandler<GetAdminActivityStatisticsQuery, Result<AdminActivityStatisticsDto>>
{
    public async Task<Result<AdminActivityStatisticsDto>> Handle(GetAdminActivityStatisticsQuery query, CancellationToken ct)
    {
        var toUtc = query.ToUtc ?? DateTime.UtcNow;
        var fromUtc = query.FromUtc ?? toUtc.AddDays(-10);

        if (fromUtc > toUtc)
            return Result<AdminActivityStatisticsDto>.Failure(
                Error.Validation(ErrorCodes.Validation.VALIDATION_ERROR, "fromUtc phai nho hon hoac bang toUtc."));

        var items = await auditLogService.GetAdminActivityStatisticsAsync(fromUtc, toUtc, ct);
        return Result<AdminActivityStatisticsDto>.Success(new AdminActivityStatisticsDto(
            fromUtc,
            toUtc,
            items.Count,
            items.Sum(x => x.ActionCount),
            items));
    }
}
