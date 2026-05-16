using Beacon.Domain.IRepository.Posts;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Posts.Commands.DeletePost;

public class DeletePostCommandHandler(IPostRepository postRepo)
    : IRequestHandler<DeletePostCommand, Result<object?>>
{
    public async Task<Result<object?>> Handle(DeletePostCommand command, CancellationToken ct)
    {
        // 1. Fetch post
        var post = await postRepo.GetByIdAsync(command.PostId, ct);
        if (post is null)
            return Result<object?>.Failure(
                Error.NotFound(ErrorCodes.Post.POST_NOT_FOUND, "Bài đăng không tồn tại."));

        // 2. Owner check
        if (post.OwnerUserId != command.CurrentUserId)
            return Result<object?>.Failure(
                Error.Forbidden(ErrorCodes.Post.POST_DELETE_DENIED, "Bạn không có quyền xóa bài đăng này."));

        // 3. Soft delete
        post.SoftDelete();

        // 4. Persist
        await postRepo.SaveChangesAsync(ct);

        return Result<object?>.Success(null);
    }
}
