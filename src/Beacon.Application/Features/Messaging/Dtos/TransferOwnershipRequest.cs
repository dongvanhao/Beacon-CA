using Beacon.Domain.Enums.Messaging;

namespace Beacon.Application.Features.Messaging.Dtos;

public record TransferOwnershipRequest(Guid TargetUserId, GroupMemberRole Role);
