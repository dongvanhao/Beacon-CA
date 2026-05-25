using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Commands.SendMessage;
using Beacon.Application.Features.Messaging.Helpers;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.LeaveGroup;

public class LeaveGroupCommandHandler(
    IMessageGroupRepository groupRepo,
    ICurrentUserService currentUser,
    ISender sender)
    : IRequestHandler<LeaveGroupCommand, Result>
{
    public async Task<Result> Handle(LeaveGroupCommand command, CancellationToken ct)
    {
        var group = await groupRepo.GetByIdWithMembersAsync(command.GroupId, ct);
        if (group is null || group.IsDeleted)
            return Result.Failure(Error.NotFound(ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND, "Không tìm thấy nhóm chat."));

        var callerMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.UserId
            && m.Status == MessageGroupMemberStatus.Joined);
        if (callerMember is null)
            return Result.Failure(Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Bạn không phải thành viên của nhóm này."));

        var joinedMemberCount = group.Members.Count(m => m.Status == MessageGroupMemberStatus.Joined);
        if (callerMember.Role == GroupMemberRole.Owner && joinedMemberCount > 1)
            return Result.Failure(Error.Validation(ErrorCodes.Validation.VALIDATION_ERROR, "Owner phải transfer ownership trước khi rời nhóm."));

        var isLastMember = joinedMemberCount == 1;
        if (!isLastMember)
        {
            var actorName = FormatName(currentUser.FamilyName, currentUser.GivenName, "Một thành viên");
            var sendResult = await sender.Send(new SendMessageCommand(
                command.GroupId,
                $"{actorName} đã rời khỏi đoạn chat".Trim(),
                null,
                null,
            MessageType.MemberLeft,
            MessageMetadataHelper.Serialize(new
            {
                actorUserId = currentUser.UserId,
                userId = currentUser.UserId
            })), ct);
            if (sendResult.IsFailure)
                return Result.Failure(sendResult.Error);
        }

        if (isLastMember)
            group.Delete();

        await groupRepo.RemoveMemberAsync(command.GroupId, currentUser.UserId, ct);
        await groupRepo.SaveChangesAsync(ct);

        return Result.Success();
    }

    private static string FormatName(string? familyName, string? givenName, string fallback)
    {
        var name = $"{familyName} {givenName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }
}
