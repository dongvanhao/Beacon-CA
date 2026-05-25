using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Commands.SendMessage;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.UpdateGroupApprovalSetting;

public class UpdateGroupApprovalSettingCommandHandler(
    IMessageGroupRepository groupRepo,
    ICurrentUserService currentUser,
    ISender sender)
    : IRequestHandler<UpdateGroupApprovalSettingCommand, Result>
{
    public async Task<Result> Handle(UpdateGroupApprovalSettingCommand command, CancellationToken ct)
    {
        var group = await groupRepo.GetByIdWithMembersAsync(command.GroupId, ct);
        if (group is null || group.IsDeleted)
            return Result.Failure(Error.NotFound(ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND, "Không tìm thấy nhóm chat."));

        if (group.Type == MessageGroupType.Direct)
            return Result.Failure(Error.Validation(ErrorCodes.Validation.VALIDATION_ERROR, "Không thể đổi yêu cầu duyệt thành viên cho chat 1-1."));

        var callerMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.UserId
            && m.Status == MessageGroupMemberStatus.Joined);
        if (callerMember is null)
            return Result.Failure(Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Bạn không phải thành viên của nhóm này."));

        if (callerMember.Role != GroupMemberRole.Owner)
            return Result.Failure(Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Chỉ owner mới được cập nhật yêu cầu duyệt thành viên."));

        var previousSetting = group.RequireApprovalToAddMembers;
        group.RequireApprovalToAddMembers = command.RequireApprovalToAddMembers;
        await groupRepo.SaveChangesAsync(ct);

        if (previousSetting != command.RequireApprovalToAddMembers)
        {
            var actorName = FormatName(currentUser.FamilyName, currentUser.GivenName, "Một thành viên");
            var action = command.RequireApprovalToAddMembers ? "bật" : "tắt";
            var sendResult = await sender.Send(new SendMessageCommand(
                command.GroupId,
                $"{actorName} đã {action} duyệt thành viên".Trim(),
                null,
                null,
                MessageType.GroupApprovalSettingChanged), ct);
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
