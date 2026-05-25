using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Commands.SendMessage;
using Beacon.Application.Features.Messaging.Helpers;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.UpdateGroupName;

public class UpdateGroupNameCommandHandler(
    IMessageGroupRepository groupRepo,
    ICurrentUserService currentUser,
    ISender sender)
    : IRequestHandler<UpdateGroupNameCommand, Result>
{
    public async Task<Result> Handle(UpdateGroupNameCommand command, CancellationToken ct)
    {
        var group = await groupRepo.GetByIdWithMembersAsync(command.GroupId, ct);
        if (group is null || group.IsDeleted)
            return Result.Failure(Error.NotFound(ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND, "Không tìm thấy nhóm chat."));

        if (group.Type == MessageGroupType.Direct)
            return Result.Failure(Error.Validation(ErrorCodes.Validation.VALIDATION_ERROR, "Không thể đổi tên chat 1-1 qua endpoint này."));

        var isMember = group.Members.Any(m => m.UserId == currentUser.UserId
            && m.Status == MessageGroupMemberStatus.Joined);
        if (!isMember)
            return Result.Failure(Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Bạn không phải thành viên của nhóm này."));

        var name = command.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(Error.Validation(ErrorCodes.Validation.VALIDATION_ERROR, "Tên nhóm không được để trống."));
        if (name.Length > 100)
            return Result.Failure(Error.Validation(ErrorCodes.Validation.VALIDATION_ERROR, "Tên nhóm không được vượt quá 100 ký tự."));

        var oldName = group.Name;
        group.Name = name;
        await groupRepo.SaveChangesAsync(ct);

        if (!string.Equals(oldName, name, StringComparison.Ordinal))
        {
            var actorName = FormatName(currentUser.FamilyName, currentUser.GivenName, "Một thành viên");
            var sendResult = await sender.Send(new SendMessageCommand(
                command.GroupId,
                $"{actorName} đã đổi tên nhóm thành {name}".Trim(),
                null,
                null,
            MessageType.GroupNameChanged,
            MessageMetadataHelper.Serialize(new
            {
                actorUserId = currentUser.UserId,
                name
            })), ct);
            if (sendResult.IsFailure)
                return Result.Failure(sendResult.Error);
        }

        return Result.Success();
    }

    private static string FormatName(string? familyName, string? givenName, string fallback)
    {
        var name = $"{familyName} {givenName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }
}
