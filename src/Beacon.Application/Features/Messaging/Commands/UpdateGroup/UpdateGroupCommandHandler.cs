using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.UpdateGroup;

public class UpdateGroupCommandHandler(
    IMessageGroupRepository groupRepo,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateGroupCommand, Result>
{
    public async Task<Result> Handle(UpdateGroupCommand command, CancellationToken ct)
    {
        var group = await groupRepo.GetByIdWithMembersAsync(command.GroupId, ct);
        if (group is null || group.IsDeleted)
            return Result.Failure(Error.NotFound(ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND, "Không tìm thấy nhóm chat."));

        if (group.Type == MessageGroupType.Direct)
            return Result.Failure(Error.Validation(ErrorCodes.Validation.VALIDATION_ERROR, "Không thể đổi tên/avatar chat 1-1 qua endpoint này."));

        var callerMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.UserId);
        if (callerMember?.Role != GroupMemberRole.Owner)
            return Result.Failure(Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Chỉ owner mới được cập nhật thông tin nhóm."));

        if (command.Name is not null) group.Name = command.Name;
        if (command.AvatarMediaObjectId.HasValue) group.AvatarMediaObjectId = command.AvatarMediaObjectId;
        await groupRepo.SaveChangesAsync(ct);

        return Result.Success();
    }
}
