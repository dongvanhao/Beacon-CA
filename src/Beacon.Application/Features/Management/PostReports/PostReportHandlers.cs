using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Management.PostReports.Dtos;
using Beacon.Application.Features.Management.Posts;
using Beacon.Application.Features.Management.Posts.Dtos;
using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Posts;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Management.PostReports;

public record ListPostReportsQuery(int Page, int PageSize, string? Search, string? Status)
    : IRequest<Result<PaginatedList<PostReportDto>>>;

public record GetPostReportByIdQuery(Guid Id) : IRequest<Result<PostReportDto>>;

public record CreatePostReportCommand(CreatePostReportRequest Request) : IRequest<Result<PostReportDto>>;

public record UpdatePostReportCommand(Guid Id, UpdatePostReportRequest Request) : IRequest<Result<PostReportDto>>;

public record DeletePostReportCommand(Guid Id) : IRequest<Result<object?>>;

public class ListPostReportsQueryHandler(
    IPostReportRepository reportRepo,
    IMediaObjectRepository mediaRepo,
    IStorageService storage)
    : IRequestHandler<ListPostReportsQuery, Result<PaginatedList<PostReportDto>>>
{
    public async Task<Result<PaginatedList<PostReportDto>>> Handle(ListPostReportsQuery query, CancellationToken ct)
    {
        if (!TryParseStatus(query.Status, out var status))
            return Result<PaginatedList<PostReportDto>>.Failure(Error.Validation(
                ErrorCodes.PostReport.INVALID_REPORT_STATUS,
                "Status khong hop le. Chi ho tro: pending, reviewed, resolved, rejected."));

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var reports = await reportRepo.ListAsync(query.Search, status, page, pageSize, ct);
        var posts = reports.Items
            .Select(x => x.Post)
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();
        var mediaMap = await ListManagedPostsQueryHandler.BuildMediaMapAsync(posts, mediaRepo, storage, ct);

        var dto = new PaginatedList<PostReportDto>(
            reports.Items.Select(report => ToDto(report, mediaMap)).ToList(),
            reports.TotalCount,
            reports.Page,
            reports.PageSize);

        return Result<PaginatedList<PostReportDto>>.Success(dto);
    }

    internal static PostReportDto ToDto(PostReport report, IReadOnlyDictionary<Guid, ManagedPostMediaDto>? mediaMap = null) => new()
    {
        Id = report.Id,
        PostId = report.PostId,
        Post = report.Post is null
            ? null
            : ListManagedPostsQueryHandler.ToDto(
                report.Post,
                mediaMap is not null && mediaMap.TryGetValue(report.Post.MediaId, out var media) ? media : null),
        ReporterUserId = report.ReporterUserId,
        Reason = report.Reason,
        Description = report.Description,
        Status = report.Status.ToString().ToLowerInvariant(),
        ReviewedByAdminId = report.ReviewedByAdminId,
        ReviewedAtUtc = report.ReviewedAtUtc,
        ResolutionNote = report.ResolutionNote,
        CreatedAtUtc = report.CreatedAtUtc,
        UpdatedAtUtc = report.UpdatedAtUtc
    };

    internal static bool TryParseStatus(string? value, out PostReportStatus? status)
    {
        status = null;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        if (!Enum.TryParse<PostReportStatus>(value.Trim(), ignoreCase: true, out var parsed))
            return false;

        status = parsed;
        return true;
    }
}

public class GetPostReportByIdQueryHandler(
    IPostReportRepository reportRepo,
    IMediaObjectRepository mediaRepo,
    IStorageService storage)
    : IRequestHandler<GetPostReportByIdQuery, Result<PostReportDto>>
{
    public async Task<Result<PostReportDto>> Handle(GetPostReportByIdQuery query, CancellationToken ct)
    {
        var report = await reportRepo.GetByIdAsync(query.Id, ct);
        if (report is null)
            return Result<PostReportDto>.Failure(Error.NotFound(ErrorCodes.PostReport.REPORT_NOT_FOUND, "Bao cao post khong ton tai."));

        var posts = report.Post is null ? [] : new List<Post> { report.Post };
        var mediaMap = await ListManagedPostsQueryHandler.BuildMediaMapAsync(posts, mediaRepo, storage, ct);
        return Result<PostReportDto>.Success(ListPostReportsQueryHandler.ToDto(report, mediaMap));
    }
}

public class CreatePostReportCommandHandler(
    IPostReportRepository reportRepo,
    IPostRepository postRepo,
    IUserRepository userRepo)
    : IRequestHandler<CreatePostReportCommand, Result<PostReportDto>>
{
    public async Task<Result<PostReportDto>> Handle(CreatePostReportCommand command, CancellationToken ct)
    {
        var request = command.Request;
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result<PostReportDto>.Failure(Error.Validation(ErrorCodes.Validation.VALIDATION_ERROR,
                "Reason khong duoc rong."));

        if (await postRepo.GetByIdForManagementAsync(request.PostId, ct) is null)
            return Result<PostReportDto>.Failure(Error.NotFound(ErrorCodes.Post.POST_NOT_FOUND, "Post khong ton tai."));

        if (!await userRepo.ExistsAsync(request.ReporterUserId, ct))
            return Result<PostReportDto>.Failure(Error.NotFound(ErrorCodes.Identity.USER_NOT_FOUND, "Reporter user khong ton tai."));

        var status = PostReportStatus.Pending;
        if (!string.IsNullOrWhiteSpace(request.Status)
            && !Enum.TryParse(request.Status, ignoreCase: true, out status))
            return Result<PostReportDto>.Failure(Error.Validation(ErrorCodes.PostReport.INVALID_REPORT_STATUS,
                "Status khong hop le."));

        var report = PostReport.Create(request.PostId, request.ReporterUserId, request.Reason, request.Description, status);
        await reportRepo.AddAsync(report, ct);
        await reportRepo.SaveChangesAsync(ct);

        return Result<PostReportDto>.Success(ListPostReportsQueryHandler.ToDto(report));
    }
}

public class UpdatePostReportCommandHandler(
    IPostReportRepository reportRepo,
    ICurrentUserService currentUser,
    IMediaObjectRepository mediaRepo,
    IStorageService storage)
    : IRequestHandler<UpdatePostReportCommand, Result<PostReportDto>>
{
    public async Task<Result<PostReportDto>> Handle(UpdatePostReportCommand command, CancellationToken ct)
    {
        var report = await reportRepo.GetByIdAsync(command.Id, ct);
        if (report is null)
            return Result<PostReportDto>.Failure(Error.NotFound(ErrorCodes.PostReport.REPORT_NOT_FOUND, "Bao cao post khong ton tai."));

        var reason = string.IsNullOrWhiteSpace(command.Request.Reason) ? report.Reason : command.Request.Reason;
        report.Update(reason, command.Request.Description ?? report.Description);

        if (!string.IsNullOrWhiteSpace(command.Request.Status))
        {
            if (!Enum.TryParse<PostReportStatus>(command.Request.Status, ignoreCase: true, out var status))
                return Result<PostReportDto>.Failure(Error.Validation(ErrorCodes.PostReport.INVALID_REPORT_STATUS,
                    "Status khong hop le."));

            report.SetStatus(status, currentUser.UserId, command.Request.ResolutionNote);
        }

        await reportRepo.SaveChangesAsync(ct);
        var posts = report.Post is null ? [] : new List<Post> { report.Post };
        var mediaMap = await ListManagedPostsQueryHandler.BuildMediaMapAsync(posts, mediaRepo, storage, ct);
        return Result<PostReportDto>.Success(ListPostReportsQueryHandler.ToDto(report, mediaMap));
    }
}

public class DeletePostReportCommandHandler(IPostReportRepository reportRepo)
    : IRequestHandler<DeletePostReportCommand, Result<object?>>
{
    public async Task<Result<object?>> Handle(DeletePostReportCommand command, CancellationToken ct)
    {
        var report = await reportRepo.GetByIdAsync(command.Id, ct);
        if (report is null)
            return Result<object?>.Failure(Error.NotFound(ErrorCodes.PostReport.REPORT_NOT_FOUND, "Bao cao post khong ton tai."));

        reportRepo.Remove(report);
        await reportRepo.SaveChangesAsync(ct);
        return Result<object?>.Success(null);
    }
}
