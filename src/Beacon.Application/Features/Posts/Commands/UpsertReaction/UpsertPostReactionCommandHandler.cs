using Beacon.Application.Features.Posts.Dtos;
using Beacon.Application.Features.Posts.Helpers;
using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository.Group;
using Beacon.Domain.IRepository.Posts;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Posts.Commands.UpsertReaction;

public class UpsertPostReactionCommandHandler(
    IPostRepository postRepo,
    IPostReactionRepository reactionRepo,
    IFriendRepository friendRepo)
    : IRequestHandler<UpsertPostReactionCommand, Result<PostReactionResponse>>
{
    public async Task<Result<PostReactionResponse>> Handle(UpsertPostReactionCommand command, CancellationToken ct)
    {
        // 1. Validate icon
        if (!ReactionIcons.IsValid(command.Icon))
            return Result<PostReactionResponse>.Failure(
                Error.Failure(ErrorCodes.Reaction.INVALID_REACTION_ICON,
                    "Icon không hợp lệ. Chỉ hỗ trợ: heart, haha, like, sad, wow"));

        // 2. Fetch post
        var post = await postRepo.GetByIdAsync(command.PostId, ct);
        if (post is null || post.IsDeleted)
            return Result<PostReactionResponse>.Failure(
                Error.NotFound(ErrorCodes.Post.POST_NOT_FOUND, "Post không tồn tại."));

        // 3. Check status
        if (post.Status != PostStatus.Active)
            return Result<PostReactionResponse>.Failure(
                Error.NotFound(ErrorCodes.Post.POST_NOT_FOUND, "Post không tồn tại."));

        // 4. Access control
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

        // 5. Upsert reaction
        var existing = await reactionRepo.GetByPostAndUserAsync(command.PostId, command.CurrentUserId, ct);
        if (existing is null)
        {
            await reactionRepo.AddAsync(PostReaction.Create(command.PostId, command.CurrentUserId, command.Icon), ct);
            await reactionRepo.SaveChangesAsync(ct);
        }
        else if (existing.Icon != command.Icon)
        {
            existing.UpdateIcon(command.Icon);
            await reactionRepo.SaveChangesAsync(ct);
        }
        // else: same icon — no-op, no SaveChangesAsync

        // 6. Load reactions for summary
        var reactions = await reactionRepo.GetByPostIdsAsync(new[] { command.PostId }, ct);

        // 7. Return
        return Result<PostReactionResponse>.Success(new PostReactionResponse
        {
            PostId = command.PostId,
            MyReaction = new MyReactionResponse { Icon = command.Icon },
            ReactionSummary = ReactionSummaryHelper.BuildSummary(reactions)
        });
    }
}
