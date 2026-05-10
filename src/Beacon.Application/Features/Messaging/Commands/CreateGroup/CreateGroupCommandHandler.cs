using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Application.Mappings.Messaging;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.CreateGroup;

public class CreateGroupCommandHandler(
    IMessageGroupRepository groupRepo,
    ICurrentUserService currentUser,
    IStorageService storage,
    MessageGroupDetailMapper mapper)
    : IRequestHandler<CreateGroupCommand, Result<MessageGroupDetailDto>>
{
    public async Task<Result<MessageGroupDetailDto>> Handle(CreateGroupCommand command, CancellationToken ct)
    {
        var group = new MessageGroup
        {
            Type = MessageGroupType.Group,
            Name = command.Name,
            AvatarMediaObjectId = command.AvatarMediaObjectId,
            CreatedAtUtc = DateTime.UtcNow
        };
        group.Members.Add(new MessageGroupMember
        {
            GroupId = group.Id,
            UserId = currentUser.UserId,
            Role = GroupMemberRole.Owner,
            JoinedAtUtc = DateTime.UtcNow
        });
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

        return Result<MessageGroupDetailDto>.Success(
            mapper.ToDetailDto(reloaded, reloaded.Name, groupAvatarUrl, memberDtos));
    }
}
