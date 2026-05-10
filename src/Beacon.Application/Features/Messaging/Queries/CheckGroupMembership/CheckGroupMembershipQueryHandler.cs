using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Queries.CheckGroupMembership;

public class CheckGroupMembershipQueryHandler(IMessageGroupRepository groupRepo)
    : IRequestHandler<CheckGroupMembershipQuery, Result<bool>>
{
    public async Task<Result<bool>> Handle(CheckGroupMembershipQuery query, CancellationToken ct)
    {
        var group = await groupRepo.GetByIdWithMembersAsync(query.GroupId, ct);
        if (group is null || group.IsDeleted)
            return Result<bool>.Failure(
                Error.NotFound(ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND, "Không tìm thấy nhóm chat."));

        if (!group.Members.Any(m => m.UserId == query.UserId))
            return Result<bool>.Failure(
                Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Bạn không thuộc nhóm chat này."));

        return Result<bool>.Success(true);
    }
}
