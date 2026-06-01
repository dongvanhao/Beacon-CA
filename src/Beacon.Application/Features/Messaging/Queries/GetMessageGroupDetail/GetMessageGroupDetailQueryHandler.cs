using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Application.Mappings.Messaging;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Queries.GetMessageGroupDetail;

public class GetMessageGroupDetailQueryHandler(
    IMessageGroupRepository groupRepo,
    IMessageGroupMemberSettingRepository settingRepo,
    ICurrentUserService currentUser,
    IStorageService storage,
    MessageGroupDetailMapper mapper)
    : IRequestHandler<GetMessageGroupDetailQuery, Result<MessageGroupDetailDto>>
{
    public async Task<Result<MessageGroupDetailDto>> Handle(
        GetMessageGroupDetailQuery query, CancellationToken ct)
    {
        var group = await groupRepo.GetByIdWithMembersAsync(query.GroupId, ct);
        if (group is null)
            return Result<MessageGroupDetailDto>.Failure(
                Error.NotFound(ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND,
                    "Không tìm thấy nhóm chat."));

        var userId = currentUser.UserId;
        var callerMember = group.Members.FirstOrDefault(m => m.UserId == userId
            && m.Status == MessageGroupMemberStatus.Joined);
        if (callerMember is null)
            return Result<MessageGroupDetailDto>.Failure(
                Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN,
                    "Bạn không phải thành viên của nhóm này."));

        var canManageMembers = callerMember.Role is GroupMemberRole.Owner or GroupMemberRole.Manager;
        var visibleMembers = group.Members
            .Where(m => canManageMembers || m.Status == MessageGroupMemberStatus.Joined)
            .ToList();

        var avatarObjects = visibleMembers
            .Select(m => m.User.AvatarMediaObject)
            .Where(a => a is not null)
            .Select(a => a!)
            .ToList();

        var urlMap = avatarObjects.Count > 0
            ? (await storage.GetMediaUrlsBatchAsync(avatarObjects, ct))
                .ToDictionary(x => x.Media.Id, x => x.Url)
            : new Dictionary<Guid, string>();

        var settings = await settingRepo.ListByGroupAsync(group.Id, ct)
            ?? [];
        var settingsByUserId = settings
            .ToDictionary(s => s.UserId, s => s.CustomName);

        var memberDtos = visibleMembers.Select(m =>
        {
            var avatarUrl = m.User.AvatarMediaObjectId.HasValue
                && urlMap.TryGetValue(m.User.AvatarMediaObjectId.Value, out var url)
                ? url : null;
            settingsByUserId.TryGetValue(m.UserId, out var customName);
            return mapper.ToMemberDto(m, customName, avatarUrl);
        }).ToList();

        // Resolve display fields so FE does not need conversation naming logic.
        var peer = memberDtos.FirstOrDefault(m => m.UserId != userId);
        string? displayName;
        string? displayAvatarUrl;

        if (group.Type == MessageGroupType.Direct)
        {
            displayName = peer is not null
                ? $"{peer.FamilyName} {peer.GivenName}".Trim()
                : "Người dùng";
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = "Người dùng";

            displayAvatarUrl = peer?.AvatarUrl;
        }
        else
        {
            displayName = !string.IsNullOrWhiteSpace(group.Name)
                ? group.Name
                : BuildGroupFallbackName(memberDtos);

            displayAvatarUrl = group.AvatarMedia is not null
                ? await storage.GeneratePresignedGetUrlAsync(group.AvatarMedia.ObjectKey, ct)
                : null;
        }

        var setting = await settingRepo.GetByGroupAndUserAsync(group.Id, userId, ct);
        var settingDto = setting is null
            ? new MessageGroupMemberSettingDto(null, false, null, null)
            : new MessageGroupMemberSettingDto(
                setting.CustomName,
                setting.IsMuted,
                setting.LastReadMessageId,
                setting.LastReadAtUtc);

        return Result<MessageGroupDetailDto>.Success(
            mapper.ToDetailDto(group, displayName, displayAvatarUrl, settingDto, memberDtos));
    }

    private static string BuildGroupFallbackName(IReadOnlyCollection<MessageGroupMemberDto> members)
    {
        var fallbackName = string.Join(", ", members
            .Select(m => $"{m.FamilyName} {m.GivenName}".Trim())
            .Where(name => name != string.Empty)
            .Take(3));

        return fallbackName != string.Empty ? fallbackName : "Nhóm chat";
    }
}
