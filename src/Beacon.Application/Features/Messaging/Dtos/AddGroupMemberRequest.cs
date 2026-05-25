namespace Beacon.Application.Features.Messaging.Dtos;

public record AddGroupMemberRequest(IReadOnlyList<Guid> TargetUserIds);
