using Beacon.Domain.IRepository.Safety;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Safety.Commands.DeleteEmergencyContact;

public class DeleteEmergencyContactCommandHandler(IEmergencyContactRepository repo)
    : IRequestHandler<DeleteEmergencyContactCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        DeleteEmergencyContactCommand cmd, CancellationToken ct)
    {
        var contact = await repo.GetByIdAsync(cmd.ContactId, ct);
        if (contact is null)
            return Result<bool>.Failure(
                Error.NotFound(ErrorCodes.Safety.EMERGENCY_CONTACT_NOT_FOUND,
                    "Không tìm thấy liên hệ khẩn cấp."));

        if (contact.UserId != cmd.UserId)
            return Result<bool>.Failure(
                Error.Forbidden(ErrorCodes.Safety.EMERGENCY_CONTACT_FORBIDDEN,
                    "Bạn không có quyền xóa liên hệ này."));

        contact.Deactivate();
        contact.Delete();
        await repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
