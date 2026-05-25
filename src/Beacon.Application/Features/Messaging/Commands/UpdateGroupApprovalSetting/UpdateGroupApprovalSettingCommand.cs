using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.UpdateGroupApprovalSetting;

public record UpdateGroupApprovalSettingCommand(Guid GroupId, bool RequireApprovalToAddMembers) : IRequest<Result>;
