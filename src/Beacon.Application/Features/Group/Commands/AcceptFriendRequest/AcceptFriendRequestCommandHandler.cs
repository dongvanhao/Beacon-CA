using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.Entities.Group;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.IRepository.Group;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Commands.AcceptFriendRequest;

public class AcceptFriendRequestCommandHandler(
    IFriendRequestRepository requestRepo,
    IFriendRepository friendRepo,
    IMessageGroupRepository groupRepo,
    ICurrentUserService currentUser)
    : IRequestHandler<AcceptFriendRequestCommand, Result>
{
    public async Task<Result> Handle(AcceptFriendRequestCommand command, CancellationToken ct)
    {
        var request = await requestRepo.GetByIdAsync(command.RequestId, ct);
        if (request is null)
            return Result.Failure(Error.NotFound(ErrorCodes.Friend.FRIEND_REQUEST_NOT_FOUND, "Lời mời kết bạn không tồn tại."));

        if (request.ReceiverId != currentUser.UserId)
            return Result.Failure(Error.Forbidden(ErrorCodes.Friend.FRIEND_REQUEST_FORBIDDEN, "Bạn không có quyền thực hiện hành động này."));

        if (request.Status != FriendRequestStatus.Pending)
            return Result.Failure(Error.Conflict(ErrorCodes.Friend.FRIEND_REQUEST_NOT_PENDING, "Lời mời kết bạn không ở trạng thái chờ."));

        // BaseEntity sets Id = Guid.NewGuid() in property initializer — FK assignments below are safe
        var group = new MessageGroup { IsPrivate = true, CreatedAtUtc = DateTime.UtcNow };
        await groupRepo.AddAsync(group, ct);

        // Create friend with canonical pair normalization
        var (u1, u2) = request.SenderId < request.ReceiverId
            ? (request.SenderId, request.ReceiverId)
            : (request.ReceiverId, request.SenderId);

        var friend = new Friend
        {
            UserId1 = u1,
            UserId2 = u2,
            Type = FriendType.Normal,
            MessageGroupId = group.Id,
            CreatedAtUtc = DateTime.UtcNow
        };
        await friendRepo.AddAsync(friend, ct);

        // Add both users as members
        group.Members.Add(new MessageGroupMember { GroupId = group.Id, UserId = request.SenderId });
        group.Members.Add(new MessageGroupMember { GroupId = group.Id, UserId = request.ReceiverId });

        // Mark accepted
        request.Status = FriendRequestStatus.Accepted;

        // Single SaveChangesAsync — EF flushes all tracked changes atomically
        await requestRepo.SaveChangesAsync(ct);

        return Result.Success();
    }
}
