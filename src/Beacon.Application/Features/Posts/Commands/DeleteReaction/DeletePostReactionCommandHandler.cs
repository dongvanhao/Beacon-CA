using Beacon.Application.Features.Posts.Dtos;
using Beacon.Application.Features.Posts.Helpers;
using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository.Group;
using Beacon.Domain.IRepository.Posts;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Posts.Commands.DeleteReaction;

public class DeletePostReactionCommandHandler(
    IPostRepository postRepo,
    IPostReactionRepository reactionRepo,
    IFriendRepository friendRepo)
    : IRequestHandler<DeletePostReactionCommand, Result<PostReactionResponse>>
{
    public async Task<Result<PostReactionResponse>> Handle(DeletePostReactionCommand command, CancellationToken ct)
    {
        // 1. Fetch post
        var post = await postRepo.GetByIdAsync(command.PostId, ct);
        if (post is null || post.IsDeleted)
            return Result<PostReactionResponse>.Failure(
                Error.NotFound(ErrorCodes.Post.POST_NOT_FOUND, "Post không tồn tại."));

        // 2. Check status
        if (post.Status != PostStatus.Active)
            return Result<PostReactionResponse>.Failure(
                Error.NotFound(ErrorCodes.Post.POST_NOT_FOUND, "Post không tồn tại."));

        // 3. Access control
        if (post.OwnerUserId != command.CurrentUserId)
        {
            if (post.Visibility == PostVisibility.Friends)
            {
                var areFriends = await friendRepo.AreFriendsAsync(command.CurrentUserId, post.OwnerUserId, ct);
                if (!areFriends)
                    return Result<PostReactionResponse>.Failure(
                        Error.Forbidden(ErrorCodes.Post.POST_ACCESS_DENIED,
                            "Bạn không có quyền xem bài đăng này."));
            }
            else
            {
                return Result<PostReactionResponse>.Failure(
                    Error.Forbidden(ErrorCodes.Post.POST_ACCESS_DENIED,
                        "Bài đăng này là riêng tư."));
            }
        }

        // 4. Delete reaction (idempotent — not found is NOT an error)
        var existing = await reactionRepo.GetByPostAndUserAsync(command.PostId, command.CurrentUserId, ct);
        if (existing is not null)
        {
            reactionRepo.Remove(existing);
            await reactionRepo.SaveChangesAsync(ct);
        }

        return Result<PostReactionResponse>.Success(new PostReactionResponse
        {
            PostId = command.PostId,
            MyReaction = null,
            ReactionSummary = ReactionSummaryHelper.BuildSummary(Array.Empty<PostReaction>())
        });
    }
}
