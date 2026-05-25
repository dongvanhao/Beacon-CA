namespace Beacon.Application.Features.Messaging.Dtos;

public record CreateGroupRequest(IReadOnlyList<Guid> MemberUserIds);
