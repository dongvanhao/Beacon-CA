using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Application.Mappings.Messaging;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Group;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.CreateGroup;

public class CreateGroupCommandHandler(
    IMessageGroupRepository groupRepo,
    IFriendRepository friendRepo,
    ICurrentUserService currentUser,
    IStorageService storage,
    MessageGroupDetailMapper mapper)
    : IRequestHandler<CreateGroupCommand, Result<MessageGroupDetailDto>>
{
    public async Task<Result<MessageGroupDetailDto>> Handle(CreateGroupCommand command, CancellationToken ct)
    {
        var requestedMemberIds = command.MemberUserIds
            .Distinct()
            .ToList();

        if (requestedMemberIds.Count == 0)
            return Result<MessageGroupDetailDto>.Failure(
                Error.Validation(ErrorCodes.Validation.VALIDATION_ERROR, "Danh sach thanh vien khong duoc rong."));

        if (requestedMemberIds.Count != command.MemberUserIds.Count)
            return Result<MessageGroupDetailDto>.Failure(
                Error.Validation(ErrorCodes.Validation.VALIDATION_ERROR, "Danh sach thanh vien khong duoc trung lap."));

        if (requestedMemberIds.Contains(currentUser.UserId))
            return Result<MessageGroupDetailDto>.Failure(
                Error.Validation(ErrorCodes.Validation.VALIDATION_ERROR, "Khong can truyen nguoi tao trong danh sach thanh vien."));

        var friendIds = await friendRepo.GetFriendIdsAsync(currentUser.UserId, requestedMemberIds, ct);
        if (friendIds.Count != requestedMemberIds.Count)
            return Result<MessageGroupDetailDto>.Failure(
                Error.Forbidden(ErrorCodes.Friend.FRIEND_NOT_FOUND, "Tat ca thanh vien duoc them vao nhom phai la ban be cua ban."));

        var now = DateTime.UtcNow;
        var group = new MessageGroup
        {
            Type = MessageGroupType.Group,
            CreatedAtUtc = now,
            RequireApprovalToAddMembers = true
        };
        group.Members.Add(new MessageGroupMember
        {
            GroupId = group.Id,
            UserId = currentUser.UserId,
            Role = GroupMemberRole.Owner,
            Status = MessageGroupMemberStatus.Joined,
            JoinedAtUtc = now
        });

        foreach (var memberId in requestedMemberIds)
        {
            group.Members.Add(new MessageGroupMember
            {
                GroupId = group.Id,
                UserId = memberId,
                Role = GroupMemberRole.Member,
                Status = MessageGroupMemberStatus.Joined,
                JoinedAtUtc = now,
                InvitedByUserId = currentUser.UserId
            });
        }

        await groupRepo.AddAsync(group, ct);
        await groupRepo.SaveChangesAsync(ct);

        var reloaded = await groupRepo.GetByIdWithMembersAsync(group.Id, ct);

        var avatarObjects = reloaded!.Members
            .Select(m => m.User.AvatarMediaObject)
            .Where(a => a is not null)
            .Select(a => a!)
            .ToList();

        var urlMap = avatarObjects.Count > 0
            ? (await storage.GetMediaUrlsBatchAsync(avatarObjects, ct)).ToDictionary(x => x.Media.Id, x => x.Url)
            : new Dictionary<Guid, string>();

        var memberDtos = reloaded.Members.Select(m =>
        {
            var avatarUrl = m.User.AvatarMediaObjectId.HasValue
                && urlMap.TryGetValue(m.User.AvatarMediaObjectId.Value, out var url) ? url : null;
            return mapper.ToMemberDto(m, avatarUrl);
        }).ToList();

        string? groupAvatarUrl = reloaded.AvatarMedia is not null
            ? await storage.GeneratePresignedGetUrlAsync(reloaded.AvatarMedia.ObjectKey, ct)
            : null;

        var displayName = BuildGroupFallbackName(memberDtos);
        var settingDto = new MessageGroupMemberSettingDto(null, false, null, null);

        return Result<MessageGroupDetailDto>.Success(
            mapper.ToDetailDto(reloaded, displayName, groupAvatarUrl, settingDto, memberDtos));
    }

    private static string BuildGroupFallbackName(IReadOnlyCollection<MessageGroupMemberDto> members)
    {
        var fallbackName = string.Join(", ", members
            .Select(m => $"{m.FamilyName} {m.GivenName}".Trim())
            .Where(name => name != string.Empty)
            .Take(3));

        return fallbackName != string.Empty ? fallbackName : "Nhom chat";
    }
}
