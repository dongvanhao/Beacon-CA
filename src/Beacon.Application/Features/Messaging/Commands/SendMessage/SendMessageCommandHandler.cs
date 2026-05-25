using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Application.Mappings.Messaging;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Enums;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Group;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Domain.IRepository.Posts;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Beacon.Application.Features.Messaging.Commands.SendMessage;

public class SendMessageCommandHandler(
    IMessageGroupRepository groupRepo,
    IMessageRepository messageRepo,
    ICurrentUserService currentUser,
    IRealtimeNotifier notifier,
    IFcmService fcmService,
    IMessageGroupPresenceTracker presenceTracker,
    IPostRepository postRepo,
    IFriendRepository friendRepo,
    ILogger<SendMessageCommandHandler> logger,
    MessageMapper mapper,
    MessagePostMapper postMapper)
    : IRequestHandler<SendMessageCommand, Result<MessageDto>>
{
    public async Task<Result<MessageDto>> Handle(SendMessageCommand command, CancellationToken ct)
    {
        Post? attachedPost = null;
        if (command.PostId.HasValue)
        {
            var postResult = await GetAccessiblePostAsync(command.PostId.Value, ct);
            if (postResult.IsFailure)
                return Result<MessageDto>.Failure(postResult.Error);

            attachedPost = postResult.Value;
        }

        var groupResult = await ResolveGroupAsync(command.GroupId, attachedPost, ct);
        if (groupResult.IsFailure)
            return Result<MessageDto>.Failure(groupResult.Error);

        var group = groupResult.Value!;
        var groupId = command.GroupId ?? group.Id;

        if (!group.Members.Any(m => m.UserId == currentUser.UserId
                && m.Status == MessageGroupMemberStatus.Joined))
            return Result<MessageDto>.Failure(
                Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Bạn không phải thành viên của nhóm này."));

        if (command.ClientMessageId is not null)
        {
            var existing = await messageRepo.GetByClientMessageIdAsync(groupId, command.ClientMessageId, ct);
            if (existing is not null)
                return Result<MessageDto>.Success(mapper.ToDto(
                    existing,
                    currentUser.FamilyName,
                    currentUser.GivenName,
                    await postMapper.ToDtoAsync(existing.Post, ct)));
        }

        var content = command.Content?.Trim() ?? string.Empty;
        var message = Message.Create(groupId, currentUser.UserId, content, command.ClientMessageId, attachedPost?.Id);
        var senderMember = group.Members.First(m => m.UserId == currentUser.UserId
            && m.Status == MessageGroupMemberStatus.Joined);
        senderMember.LastSeenMessageId = message.Id;

        await messageRepo.AddAsync(message, ct);
        await messageRepo.SaveChangesAsync(ct);

        var dto = mapper.ToDto(
            message,
            currentUser.FamilyName,
            currentUser.GivenName,
            await postMapper.ToDtoAsync(attachedPost, ct));

        var recipientUserIds = group.Members
            .Where(m => m.Status == MessageGroupMemberStatus.Joined)
            .Select(m => m.UserId)
            .Distinct()
            .ToArray();

        await notifier.NotifyNewMessageAsync(groupId, dto, recipientUserIds, ct);

        var fcmRecipientUserIds = recipientUserIds
            .Where(id => id != currentUser.UserId)
            .Where(id => !presenceTracker.IsUserInGroup(id, groupId))
            .Distinct()
            .ToArray();

        foreach (var member in group.Members.Where(m => m.Status == MessageGroupMemberStatus.Joined))
        {
            var unreadCount = await messageRepo.CountUnreadAsync(groupId, member.LastSeenMessageId, ct);
            await notifier.NotifyUnreadMessageCountAsync(member.UserId, groupId, unreadCount, ct);
        }

        if (fcmRecipientUserIds.Length > 0)
        {
            var senderName = $"{currentUser.GivenName} {currentUser.FamilyName}".Trim();
            var title = string.IsNullOrWhiteSpace(senderName) ? "Tin nhắn mới" : senderName;
            var body = string.IsNullOrWhiteSpace(content) ? "Bạn có tin nhắn mới" : content;

            var fcmData = new Dictionary<string, string>
            {
                ["type"] = "MESSAGE_NEW",
                ["messageGroupId"] = groupId.ToString(),
                ["messageId"] = message.Id.ToString(),
                ["senderUserId"] = currentUser.UserId.ToString()
            };

            foreach (var userId in fcmRecipientUserIds)
            {
                try
                {
                    await fcmService.SendToUserAsync(userId, title, body, fcmData, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "FCM delivery failed for user {UserId}", userId);
                }
            }
        }

        return Result<MessageDto>.Success(dto);
    }

    private async Task<Result<Post>> GetAccessiblePostAsync(Guid postId, CancellationToken ct)
    {
        var post = await postRepo.GetByIdAsync(postId, ct);
        if (post is null || post.IsDeleted || post.Status != PostStatus.Active)
            return Result<Post>.Failure(
                Error.NotFound(ErrorCodes.Post.POST_NOT_FOUND, "Bài đăng không tồn tại."));

        if (post.OwnerUserId == currentUser.UserId)
            return Result<Post>.Success(post);

        if (post.Visibility != PostVisibility.Friends)
            return Result<Post>.Failure(
                Error.Forbidden(ErrorCodes.Post.POST_ACCESS_DENIED, "Bài đăng này là riêng tư."));

        var areFriends = await friendRepo.AreFriendsAsync(currentUser.UserId, post.OwnerUserId, ct);
        if (!areFriends)
            return Result<Post>.Failure(
                Error.Forbidden(ErrorCodes.Post.POST_ACCESS_DENIED, "Bạn không có quyền xem bài đăng này."));

        return Result<Post>.Success(post);
    }

    private async Task<Result<MessageGroup>> ResolveGroupAsync(Guid? groupId, Post? attachedPost, CancellationToken ct)
    {
        if (groupId.HasValue)
        {
            var group = await groupRepo.GetByIdAsync(groupId.Value, ct);
            return group is null
                ? Result<MessageGroup>.Failure(Error.NotFound(
                    ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND,
                    "Nhóm chat không tồn tại hoặc đã bị xóa."))
                : Result<MessageGroup>.Success(group);
        }

        if (attachedPost is null)
            return Result<MessageGroup>.Failure(Error.Validation(
                ErrorCodes.Validation.VALIDATION_ERROR,
                "Cần truyền groupId hoặc postId để gửi tin nhắn."));

        if (attachedPost.OwnerUserId == currentUser.UserId)
            return Result<MessageGroup>.Failure(Error.Validation(
                ErrorCodes.Validation.VALIDATION_ERROR,
                "Không thể tự suy ra nhóm chat khi post thuộc về chính bạn."));

        var directGroup = await groupRepo.GetPrivateGroupBetweenAsync(
            currentUser.UserId,
            attachedPost.OwnerUserId,
            ct);

        return directGroup is null
            ? Result<MessageGroup>.Failure(Error.NotFound(
                ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND,
                "Không tìm thấy nhóm chat trực tiếp với chủ bài đăng."))
            : Result<MessageGroup>.Success(directGroup);
    }
}
