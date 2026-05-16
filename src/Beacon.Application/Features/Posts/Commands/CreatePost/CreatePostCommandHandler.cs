using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Posts.Dtos;
using Beacon.Application.Mappings.Posts;
using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository.Posts;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Posts.Commands.CreatePost;

public class CreatePostCommandHandler(
    IPostRepository postRepo,
    IMediaObjectRepository mediaRepo,
    IStorageService storage,
    PostDtoMapper mapper)
    : IRequestHandler<CreatePostCommand, Result<PostResponse>>
{
    public async Task<Result<PostResponse>> Handle(CreatePostCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var currentUserId = command.CurrentUserId;

        // 1. Parse visibility
        PostVisibility visibility;
        if (string.IsNullOrEmpty(request.Visibility) || request.Visibility.Equals("friends", StringComparison.OrdinalIgnoreCase))
        {
            visibility = PostVisibility.Friends;
        }
        else if (request.Visibility.Equals("private", StringComparison.OrdinalIgnoreCase))
        {
            visibility = PostVisibility.Private;
        }
        else
        {
            return Result<PostResponse>.Failure(
                Error.Validation(ErrorCodes.Post.INVALID_VISIBILITY,
                    "Visibility không hợp lệ. Chỉ hỗ trợ: 'friends' hoặc 'private'."));
        }

        // 2. Fetch media
        var media = await mediaRepo.GetByIdAsync(request.MediaId, ct);
        if (media is null)
            return Result<PostResponse>.Failure(
                Error.NotFound(ErrorCodes.Storage.MEDIA_NOT_FOUND, "Media không tồn tại."));

        // 3. Check media ownership
        if (media.UploadProviderByUserId != currentUserId)
            return Result<PostResponse>.Failure(
                Error.Forbidden(ErrorCodes.Post.MEDIA_ACCESS_DENIED, "Bạn không có quyền sử dụng media này."));

        // 4. Check media readiness
        if (!media.IsReadyForPost())
            return Result<PostResponse>.Failure(
                Error.Failure(ErrorCodes.Post.MEDIA_NOT_READY, "Media chưa sẵn sàng để đăng."));

        // 5. Check media type
        if (media.MediaType != MediaType.Image && media.MediaType != MediaType.Video)
            return Result<PostResponse>.Failure(
                Error.Failure(ErrorCodes.Post.UNSUPPORTED_MEDIA_TYPE, "Loại media không được hỗ trợ. Chỉ hỗ trợ ảnh và video."));

        // 6. Video duration check
        if (media.MediaType == MediaType.Video)
        {
            if (media.DurationSeconds == null || media.DurationSeconds < 5 || media.DurationSeconds > 10)
                return Result<PostResponse>.Failure(
                    Error.Failure(ErrorCodes.Post.INVALID_VIDEO_DURATION, "Video phải có độ dài từ 5 đến 10 giây."));
        }

        // 7. Create post entity
        var post = Post.Create(currentUserId, request.MediaId, request.Caption, visibility);

        // 8. Persist
        await postRepo.AddAsync(post, ct);
        await postRepo.SaveChangesAsync(ct);

        // 9. Get media URL
        var (url, thumbUrl) = await storage.GetMediaUrlsAsync(media, ct);
        var mediaResponse = mapper.ToMediaResponse(media, url, thumbUrl);

        // 10. Return response
        return Result<PostResponse>.Success(mapper.ToPostResponse(post, mediaResponse));
    }
}
