using Beacon.Application.Features.Group.Dtos;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Commands.MarkAllNotificationsRead;

public class MarkAllNotificationsReadCommandHandler(INotificationRepository repo)
    : IRequestHandler<MarkAllNotificationsReadCommand, Result<MarkReadResponse>>
{
    public async Task<Result<MarkReadResponse>> Handle(
        MarkAllNotificationsReadCommand cmd, CancellationToken ct)
    {
        await repo.MarkAllReadAsync(cmd.CurrentUserId, ct);
        return Result<MarkReadResponse>.Success(new MarkReadResponse(0));
    }
}
