using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Management.Posts.Dtos;
using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Entities.Storage;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository.Posts;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Management.Posts;

public record ListManagedPostsQuery(int Page, int PageSize, string? Search, bool? IsDeleted)
    : IRequest<Result<PaginatedList<PostManagementDto>>>;

public record GetManagedPostByIdQuery(Guid Id) : IRequest<Result<PostManagementDto>>;

public record UpdateManagedPostCommand(Guid Id, UpdateManagedPostRequest Request)
    : IRequest<Result<PostManagementDto>>;

public record SoftDeleteManagedPostCommand(Guid Id, string? Reason) : IRequest<Result<object?>>;

public class ListManagedPostsQueryHandler(
    IPostRepository postRepo,
    IMediaObjectRepository mediaRepo,
    IStorageService storage)
    : IRequestHandler<ListManagedPostsQuery, Result<PaginatedList<PostManagementDto>>>
{
    public async Task<Result<PaginatedList<PostManagementDto>>> Handle(ListManagedPostsQuery query, CancellationToken ct)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var posts = await postRepo.ListForManagementAsync(query.Search, query.IsDeleted, page, pageSize, ct);
        var mediaMap = await BuildMediaMapAsync(posts.Items, mediaRepo, storage, ct);

        var dto = new PaginatedList<PostManagementDto>(
            posts.Items.Select(post => ToDto(post, mediaMap.GetValueOrDefault(post.MediaId))).ToList(),
            posts.TotalCount,
            posts.Page,
            posts.PageSize);

        return Result<PaginatedList<PostManagementDto>>.Success(dto);
    }

    internal static PostManagementDto ToDto(Post post, ManagedPostMediaDto? media = null) => new()
    {
        Id = post.Id,
        OwnerUserId = post.OwnerUserId,
        MediaId = post.MediaId,
        Media = media,
        Caption = post.Caption,
        Visibility = post.Visibility.ToString().ToLowerInvariant(),
        Status = post.Status.ToString().ToLowerInvariant(),
        DailySafetyRecordId = post.DailySafetyRecordId,
        Latitude = post.Latitude,
        Longitude = post.Longitude,
        DeletedAtUtc = post.DeletedAtUtc,
        DeletedReason = post.DeletedReason,
        CreatedAtUtc = post.CreatedAtUtc,
        UpdatedAtUtc = post.UpdatedAtUtc
    };

    internal static async Task<Dictionary<Guid, ManagedPostMediaDto>> BuildMediaMapAsync(
        IReadOnlyCollection<Post> posts,
        IMediaObjectRepository mediaRepo,
        IStorageService storage,
        CancellationToken ct)
    {
        var mediaIds = posts.Select(p => p.MediaId).Distinct().ToList();
        if (mediaIds.Count == 0)
            return new Dictionary<Guid, ManagedPostMediaDto>();

        var medias = await mediaRepo.ListByIdsIncludeDeletedAsync(mediaIds, ct);
        var urlRows = await storage.GetMediaUrlsBatchAsync(medias, ct);

        return urlRows.ToDictionary(
            x => x.Media.Id,
            x => ToMediaDto(x.Media, x.Url, x.ThumbUrl));
    }

    private static ManagedPostMediaDto ToMediaDto(MediaObject media, string url, string? thumbUrl) => new()
    {
        Id = media.Id,
        Url = url,
        Type = media.MediaType.ToString().ToLowerInvariant(),
        ThumbnailUrl = thumbUrl,
        DurationSeconds = media.DurationSeconds,
        Width = media.Width,
        Height = media.Height
    };
}

public class GetManagedPostByIdQueryHandler(
    IPostRepository postRepo,
    IMediaObjectRepository mediaRepo,
    IStorageService storage)
    : IRequestHandler<GetManagedPostByIdQuery, Result<PostManagementDto>>
{
    public async Task<Result<PostManagementDto>> Handle(GetManagedPostByIdQuery query, CancellationToken ct)
    {
        var post = await postRepo.GetByIdForManagementAsync(query.Id, ct);
        if (post is null)
            return Result<PostManagementDto>.Failure(Error.NotFound(ErrorCodes.Post.POST_NOT_FOUND, "Post khong ton tai."));

        var mediaMap = await ListManagedPostsQueryHandler.BuildMediaMapAsync([post], mediaRepo, storage, ct);
        return Result<PostManagementDto>.Success(
            ListManagedPostsQueryHandler.ToDto(post, mediaMap.GetValueOrDefault(post.MediaId)));
    }
}

public class UpdateManagedPostCommandHandler(
    IPostRepository postRepo,
    IMediaObjectRepository mediaRepo,
    IStorageService storage)
    : IRequestHandler<UpdateManagedPostCommand, Result<PostManagementDto>>
{
    public async Task<Result<PostManagementDto>> Handle(UpdateManagedPostCommand command, CancellationToken ct)
    {
        var post = await postRepo.GetByIdForManagementAsync(command.Id, ct);
        if (post is null)
            return Result<PostManagementDto>.Failure(Error.NotFound(ErrorCodes.Post.POST_NOT_FOUND, "Post khong ton tai."));

        var visibility = post.Visibility;
        if (!string.IsNullOrWhiteSpace(command.Request.Visibility)
            && !TryParseEnum(command.Request.Visibility, out visibility))
            return Result<PostManagementDto>.Failure(Error.Validation(ErrorCodes.Post.INVALID_VISIBILITY,
                "Visibility khong hop le. Chi ho tro: friends hoac private."));

        var status = post.Status;
        if (!string.IsNullOrWhiteSpace(command.Request.Status)
            && !TryParseEnum(command.Request.Status, out status))
            return Result<PostManagementDto>.Failure(Error.Validation(ErrorCodes.Post.INVALID_STATUS,
                "Status khong hop le. Chi ho tro: active hoac hidden."));

        post.UpdateContent(command.Request.Caption ?? post.Caption, visibility,
            command.Request.Latitude ?? post.Latitude,
            command.Request.Longitude ?? post.Longitude);
        post.UpdateStatus(status);

        await postRepo.SaveChangesAsync(ct);
        var mediaMap = await ListManagedPostsQueryHandler.BuildMediaMapAsync([post], mediaRepo, storage, ct);
        return Result<PostManagementDto>.Success(
            ListManagedPostsQueryHandler.ToDto(post, mediaMap.GetValueOrDefault(post.MediaId)));
    }

    private static bool TryParseEnum<TEnum>(string value, out TEnum parsed)
        where TEnum : struct, Enum
        => Enum.TryParse(value.Trim(), ignoreCase: true, out parsed);
}

public class SoftDeleteManagedPostCommandHandler(IPostRepository postRepo)
    : IRequestHandler<SoftDeleteManagedPostCommand, Result<object?>>
{
    public async Task<Result<object?>> Handle(SoftDeleteManagedPostCommand command, CancellationToken ct)
    {
        var post = await postRepo.GetByIdForManagementAsync(command.Id, ct);
        if (post is null)
            return Result<object?>.Failure(Error.NotFound(ErrorCodes.Post.POST_NOT_FOUND, "Post khong ton tai."));

        post.SoftDelete(command.Reason);
        await postRepo.SaveChangesAsync(ct);
        return Result<object?>.Success(null);
    }
}
