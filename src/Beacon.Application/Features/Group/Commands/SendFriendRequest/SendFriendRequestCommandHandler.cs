using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Group.Dtos;
using Beacon.Application.Mappings.Group;
using Beacon.Domain.Entities.Group;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Commands.SendFriendRequest;

public class SendFriendRequestCommandHandler(
    IFriendRequestRepository requestRepo,
    IFriendRepository friendRepo,
    ICurrentUserService currentUser,
    FriendRequestMapper mapper)
    : IRequestHandler<SendFriendRequestCommand, Result<FriendRequestDto>>
{
    public async Task<Result<FriendRequestDto>> Handle(
        SendFriendRequestCommand command, CancellationToken ct)
    {
        var senderId = currentUser.UserId;

        if (senderId == command.ReceiverId)
            return Result<FriendRequestDto>.Failure(
                Error.Validation(ErrorCodes.Friend.SELF_FRIEND_REQUEST, "Không thể gửi lời mời kết bạn cho chính mình."));

        if (await requestRepo.HasPendingBetweenAsync(senderId, command.ReceiverId, ct))
            return Result<FriendRequestDto>.Failure(
                Error.Conflict(ErrorCodes.Friend.FRIEND_REQUEST_DUPLICATE, "Đã có lời mời kết bạn đang chờ xử lý giữa hai người."));

        if (await friendRepo.AreFriendsAsync(senderId, command.ReceiverId, ct))
            return Result<FriendRequestDto>.Failure(
                Error.Conflict(ErrorCodes.Friend.ALREADY_FRIENDS, "Hai người đã là bạn bè."));

        var request = new FriendRequest
        {
            SenderId = senderId,
            ReceiverId = command.ReceiverId,
            Status = FriendRequestStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        await requestRepo.AddAsync(request, ct);
        await requestRepo.SaveChangesAsync(ct);

        return Result<FriendRequestDto>.Success(mapper.ToDto(request, currentUser.FamilyName, currentUser.GivenName, null));
    }
}
