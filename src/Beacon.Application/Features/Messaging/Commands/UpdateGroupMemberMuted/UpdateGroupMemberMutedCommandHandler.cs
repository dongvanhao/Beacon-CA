using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.UpdateGroupMemberMuted;

public class UpdateGroupMemberMutedCommandHandler(
    IMessageGroupRepository groupRepo,
    IMessageGroupMemberSettingRepository settingRepo,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateGroupMemberMutedCommand, Result>
{
    public async Task<Result> Handle(UpdateGroupMemberMutedCommand command, CancellationToken ct)
    {
        var group = await groupRepo.GetByIdWithMembersAsync(command.GroupId, ct);
        if (group is null || group.IsDeleted)
            return Result.Failure(Error.NotFound(ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND, "Không tìm thấy nhóm chat."));

        var callerMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.UserId
            && m.Status == MessageGroupMemberStatus.Joined);
        if (callerMember is null)
            return Result.Failure(Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Bạn không phải thành viên của nhóm này."));

        var setting = await settingRepo.GetByGroupAndUserAsync(command.GroupId, currentUser.UserId, ct);
        if (setting is null)
        {
            setting = MessageGroupMemberSetting.Create(command.GroupId, currentUser.UserId);
            await settingRepo.AddAsync(setting, ct);
        }

        setting.SetMuted(command.IsMuted);
        await settingRepo.SaveChangesAsync(ct);

        return Result.Success();
    }
}
