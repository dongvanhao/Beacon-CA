using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Commands.SendMessage;
using Beacon.Application.Features.Messaging.Helpers;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.UpdateGroupMemberCustomName;

public class UpdateGroupMemberCustomNameCommandHandler(
    IMessageGroupRepository groupRepo,
    IMessageGroupMemberSettingRepository settingRepo,
    ICurrentUserService currentUser,
    ISender sender)
    : IRequestHandler<UpdateGroupMemberCustomNameCommand, Result>
{
    public async Task<Result> Handle(UpdateGroupMemberCustomNameCommand command, CancellationToken ct)
    {
        var group = await groupRepo.GetByIdWithMembersAsync(command.GroupId, ct);
        if (group is null || group.IsDeleted)
            return Result.Failure(Error.NotFound(ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND, "Không tìm thấy nhóm chat."));

        var callerMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.UserId
            && m.Status == MessageGroupMemberStatus.Joined);
        if (callerMember is null)
            return Result.Failure(Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Bạn không phải thành viên của nhóm này."));

        var targetMember = group.Members.FirstOrDefault(m => m.UserId == command.TargetUserId
            && m.Status == MessageGroupMemberStatus.Joined);
        if (targetMember is null)
            return Result.Failure(Error.NotFound(ErrorCodes.Messaging.GROUP_MEMBER_NOT_FOUND, "Người dùng không phải thành viên của nhóm."));

        var customName = string.IsNullOrWhiteSpace(command.CustomName)
            ? null
            : command.CustomName.Trim();

        if (customName?.Length > 100)
            return Result.Failure(Error.Validation(ErrorCodes.Validation.VALIDATION_ERROR, "Biệt danh không được vượt quá 100 ký tự."));

        var setting = await settingRepo.GetByGroupAndUserAsync(command.GroupId, command.TargetUserId, ct);
        if (setting is null)
        {
            setting = MessageGroupMemberSetting.Create(command.GroupId, command.TargetUserId);
            await settingRepo.AddAsync(setting, ct);
        }

        setting.UpdateCustomName(customName);
        await settingRepo.SaveChangesAsync(ct);

        var actorName = FormatName(currentUser.FamilyName, currentUser.GivenName, "Một thành viên");
        var targetName = $"{targetMember.User.FamilyName} {targetMember.User.GivenName}".Trim();
        var displayTarget = string.IsNullOrWhiteSpace(targetName) ? "thành viên" : targetName;
        var content = customName is null
            ? $"{actorName} đã xóa biệt danh của {displayTarget}"
            : $"{actorName} đã đổi biệt danh của {displayTarget} thành {customName}";

        var sendResult = await sender.Send(new SendMessageCommand(
            command.GroupId,
            content.Trim(),
            null,
            null,
            MessageType.MemberNicknameChanged,
            MessageMetadataHelper.Serialize(new
            {
                actorUserId = currentUser.UserId,
                userId = command.TargetUserId,
                customName
            })), ct);

        return sendResult.IsFailure
            ? Result.Failure(sendResult.Error)
            : Result.Success();
    }

    private static string FormatName(string? familyName, string? givenName, string fallback)
    {
        var name = $"{familyName} {givenName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }
}
