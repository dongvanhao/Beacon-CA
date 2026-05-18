using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Posts.Dtos;
using Beacon.Application.Mappings.Posts;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository.Posts;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Posts.Commands.UpdatePost;

public class UpdatePostCommandHandler(
    IPostRepository postRepo,
    IMediaObjectRepository mediaRepo,
    IStorageService storage,
    PostDtoMapper mapper)
    : IRequestHandler<UpdatePostCommand, Result<PostResponse>>
{
    public async Task<Result<PostResponse>> Handle(UpdatePostCommand command, CancellationToken ct)
    {
        var request = command.Request;

        // 1. Fetch post
        var post = await postRepo.GetByIdAsync(command.PostId, ct);
        if (post is null)
            return Result<PostResponse>.Failure(
                Error.NotFound(ErrorCodes.Post.POST_NOT_FOUND, "Bài đăng không tồn tại."));

        // 2. Owner check
        if (post.OwnerUserId != command.CurrentUserId)
            return Result<PostResponse>.Failure(
                Error.Forbidden(ErrorCodes.Post.POST_UPDATE_DENIED, "Bạn không có quyền chỉnh sửa bài đăng này."));

        // 3. Parse new visibility
        PostVisibility newVisibility;
        if (request.Visibility == null)
        {
            newVisibility = post.Visibility;
        }
        else if (request.Visibility.Equals("friends", StringComparison.OrdinalIgnoreCase))
        {
            newVisibility = PostVisibility.Friends;
        }
        else if (request.Visibility.Equals("private", StringComparison.OrdinalIgnoreCase))
        {
            newVisibility = PostVisibility.Private;
        }
        else
        {
            return Result<PostResponse>.Failure(
                Error.Validation(ErrorCodes.Post.INVALID_VISIBILITY,
                    "Visibility không hợp lệ. Chỉ hỗ trợ: 'friends' hoặc 'private'."));
        }

        // 4. Update post content
        var newCaption = request.Caption ?? post.Caption;
        var newLatitude = request.Latitude ?? post.Latitude;
        var newLongitude = request.Longitude ?? post.Longitude;
        post.UpdateContent(newCaption, newVisibility, newLatitude, newLongitude);

        // 5. Persist
        await postRepo.SaveChangesAsync(ct);

        // 6. Fetch media for response
        var media = await mediaRepo.GetByIdAsync(post.MediaId, ct);
        if (media is null)
        {
            // Shouldn't happen, but fall back to a minimal media response
            var fallbackMedia = new MediaInPostResponse { Id = post.MediaId, Url = string.Empty, Type = "unknown" };
            return Result<PostResponse>.Success(mapper.ToPostResponse(post, fallbackMedia));
        }

        var (url, thumbUrl) = await storage.GetMediaUrlsAsync(media, ct);
        var mediaResponse = mapper.ToMediaResponse(media, url, thumbUrl);

        return Result<PostResponse>.Success(mapper.ToPostResponse(post, mediaResponse));
    }
}
