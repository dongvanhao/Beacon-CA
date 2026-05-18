using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Posts.Dtos;
using Beacon.Application.Features.Posts.Helpers;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Group;
using Beacon.Domain.IRepository.Posts;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Beacon.Application.Features.Posts.Commands.UpsertReaction;

public class UpsertPostReactionCommandHandler(
    IPostRepository postRepo,
    IPostReactionRepository reactionRepo,
    IFriendRepository friendRepo,
    IUserRepository userRepo,
    IMediaObjectRepository mediaRepo,
    IStorageService storage,
    IFcmService fcmService,
    ILogger<UpsertPostReactionCommandHandler> logger)
    : IRequestHandler<UpsertPostReactionCommand, Result<PostReactionResponse>>
{
    public async Task<Result<PostReactionResponse>> Handle(UpsertPostReactionCommand command, CancellationToken ct)
    {
        if (!ReactionIcons.IsValid(command.Icon))
            return Result<PostReactionResponse>.Failure(
                Error.Failure(
                    ErrorCodes.Reaction.INVALID_REACTION_ICON,
                    $"Icon must be at most {ReactionIcons.MaxIconLength} characters and must not contain the '{ReactionIcons.Separator}' separator."));

        var post = await postRepo.GetByIdAsync(command.PostId, ct);
        if (post is null || post.IsDeleted || post.Status != PostStatus.Active)
            return Result<PostReactionResponse>.Failure(
                Error.NotFound(ErrorCodes.Post.POST_NOT_FOUND, "Post does not exist."));

        if (post.OwnerUserId != command.CurrentUserId)
        {
            if (post.Visibility == PostVisibility.Friends)
            {
                var areFriends = await friendRepo.AreFriendsAsync(command.CurrentUserId, post.OwnerUserId, ct);
                if (!areFriends)
                    return Result<PostReactionResponse>.Failure(
                        Error.Forbidden(ErrorCodes.Post.POST_ACCESS_DENIED, "You cannot react to this post."));
            }
            else
            {
                return Result<PostReactionResponse>.Failure(
                    Error.Forbidden(ErrorCodes.Post.POST_ACCESS_DENIED, "This post is private."));
            }
        }

        var existing = await reactionRepo.GetByPostAndUserAsync(command.PostId, command.CurrentUserId, ct);
        PostReaction myReaction;
        if (existing is null)
        {
            myReaction = PostReaction.Create(command.PostId, command.CurrentUserId, command.Icon);
            await reactionRepo.AddAsync(myReaction, ct);
        }
        else
        {
            existing.AppendIcon(command.Icon);
            myReaction = existing;
        }

        await reactionRepo.SaveChangesAsync(ct);

        if (post.OwnerUserId != command.CurrentUserId)
            await SendReactionFcmToPostOwnerAsync(post.OwnerUserId, command, ct);

        return Result<PostReactionResponse>.Success(new PostReactionResponse
        {
            PostId = command.PostId,
            MyReaction = new MyReactionResponse { Icon = myReaction.Icon },
            ReactionSummary = ReactionSummaryHelper.BuildSummary(new[] { myReaction })
        });
    }

    private async Task SendReactionFcmToPostOwnerAsync(
        Guid ownerUserId,
        UpsertPostReactionCommand command,
        CancellationToken ct)
    {
        var reactor = await userRepo.GetByIdAsync(command.CurrentUserId, ct);
        var reactorDisplayName = reactor is null ? string.Empty : $"{reactor.FamilyName} {reactor.GivenName}".Trim();
        var reactorAvatarUrl = await ResolveAvatarUrlAsync(reactor, ct);

        var title = "Co reaction moi";
        var body = string.IsNullOrWhiteSpace(reactorDisplayName)
            ? $"{command.Icon} tren bai viet cua ban."
            : $"{reactorDisplayName} tha {command.Icon} tren bai viet cua ban.";
        var fcmData = new Dictionary<string, string>
        {
            ["type"] = "POST_REACTION",
            ["postId"] = command.PostId.ToString(),
            ["reactionIcon"] = command.Icon,
            ["reactorUserId"] = command.CurrentUserId.ToString()
        };

        if (!string.IsNullOrWhiteSpace(reactorDisplayName))
            fcmData["reactorDisplayName"] = reactorDisplayName;

        if (!string.IsNullOrWhiteSpace(reactorAvatarUrl))
            fcmData["reactorAvatarUrl"] = reactorAvatarUrl;

        try
        {
            await fcmService.SendToUserAsync(ownerUserId, title, body, fcmData, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "FCM post reaction delivery failed for postOwnerId={OwnerUserId}, postId={PostId}, reactorUserId={ReactorUserId}",
                ownerUserId,
                command.PostId,
                command.CurrentUserId);
        }
    }

    private async Task<string?> ResolveAvatarUrlAsync(User? user, CancellationToken ct)
    {
        if (user?.AvatarMediaObjectId is not { } avatarMediaObjectId)
            return null;

        var avatarMedia = await mediaRepo.GetByIdAsync(avatarMediaObjectId, ct);
        return avatarMedia is null
            ? null
            : await storage.GeneratePresignedGetUrlAsync(avatarMedia.ObjectKey, ct);
    }
}
