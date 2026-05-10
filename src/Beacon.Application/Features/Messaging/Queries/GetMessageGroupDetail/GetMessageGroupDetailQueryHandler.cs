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
        if (!group.Members.Any(m => m.UserId == userId))
            return Result<MessageGroupDetailDto>.Failure(
                Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN,
                    "Bạn không phải thành viên của nhóm này."));

        var avatarObjects = group.Members
            .Select(m => m.User.AvatarMediaObject)
            .Where(a => a is not null)
            .Select(a => a!)
            .ToList();

        var urlMap = avatarObjects.Count > 0
            ? (await storage.GetMediaUrlsBatchAsync(avatarObjects, ct))
                .ToDictionary(x => x.Media.Id, x => x.Url)
            : new Dictionary<Guid, string>();

        var memberDtos = group.Members.Select(m =>
        {
            var avatarUrl = m.User.AvatarMediaObjectId.HasValue
                && urlMap.TryGetValue(m.User.AvatarMediaObjectId.Value, out var url)
                ? url : null;
            return mapper.ToMemberDto(m, avatarUrl);
        }).ToList();

        // Resolve tên và ảnh hiển thị:
        // - Ưu tiên tên/ảnh tuỳ chỉnh của group.
        // - Chat 1-1 (IsPrivate): fallback sang tên + avatar của người kia.
        string? displayName = group.Name;
        string? displayAvatarUrl = group.AvatarMedia is not null
            ? await storage.GeneratePresignedGetUrlAsync(group.AvatarMedia.ObjectKey, ct)
            : null;

        if (group.Type == MessageGroupType.Direct && (displayName is null || displayAvatarUrl is null))
        {
            var peer = memberDtos.FirstOrDefault(m => m.UserId != userId);
            displayName ??= peer is not null
                ? $"{peer.FamilyName} {peer.GivenName}".Trim()
                : null;
            displayAvatarUrl ??= peer?.AvatarUrl;
        }

        return Result<MessageGroupDetailDto>.Success(
            mapper.ToDetailDto(group, displayName, displayAvatarUrl, memberDtos));
    }
}
