using Beacon.Domain.IRepository.Safety;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Safety.Commands.SetPrimaryEmergencyContact;

public class SetPrimaryEmergencyContactCommandHandler(IEmergencyContactRepository repo)
    : IRequestHandler<SetPrimaryEmergencyContactCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        SetPrimaryEmergencyContactCommand cmd, CancellationToken ct)
    {
        var contact = await repo.GetByIdAsync(cmd.ContactId, ct);
        if (contact is null)
            return Result<bool>.Failure(
                Error.NotFound(ErrorCodes.Safety.EMERGENCY_CONTACT_NOT_FOUND,
                    "Không tìm thấy liên hệ khẩn cấp."));

        if (contact.UserId != cmd.UserId)
            return Result<bool>.Failure(
                Error.Forbidden(ErrorCodes.Safety.EMERGENCY_CONTACT_FORBIDDEN,
                    "Bạn không có quyền thao tác liên hệ này."));

        if (!contact.IsActive)
            return Result<bool>.Failure(
                Error.Validation(ErrorCodes.Safety.EMERGENCY_CONTACT_INACTIVE,
                    "Liên hệ khẩn cấp này đã bị vô hiệu hóa."));

        var currentPrimary = await repo.GetPrimaryByUserIdAsync(cmd.UserId, ct);
        if (currentPrimary is not null && currentPrimary.Id != cmd.ContactId)
            currentPrimary.ClearPrimary();

        contact.SetAsPrimary();
        await repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
