using Beacon.Application.Features.Safety.Dtos;
using Beacon.Application.Mappings.Safety;
using Beacon.Domain.IRepository.Safety;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Safety.Commands.SetPrimaryEmergencyContact;

public class SetPrimaryEmergencyContactCommandHandler(
    IEmergencyContactRepository repo,
    EmergencyContactMapper mapper)
    : IRequestHandler<SetPrimaryEmergencyContactCommand, Result<EmergencyContactDto>>
{
    public async Task<Result<EmergencyContactDto>> Handle(
        SetPrimaryEmergencyContactCommand cmd, CancellationToken ct)
    {
        var contact = await repo.GetByIdAsync(cmd.ContactId, ct);
        if (contact is null)
            return Result<EmergencyContactDto>.Failure(
                Error.NotFound(ErrorCodes.Safety.EMERGENCY_CONTACT_NOT_FOUND,
                    "Không tìm thấy liên hệ khẩn cấp."));

        if (contact.UserId != cmd.UserId)
            return Result<EmergencyContactDto>.Failure(
                Error.Forbidden(ErrorCodes.Safety.EMERGENCY_CONTACT_FORBIDDEN,
                    "Bạn không có quyền thao tác liên hệ này."));

        var currentPrimary = await repo.GetPrimaryByUserIdAsync(cmd.UserId, ct);
        if (currentPrimary is not null && currentPrimary.Id != cmd.ContactId)
            currentPrimary.ClearPrimary();

        contact.SetAsPrimary();
        await repo.SaveChangesAsync(ct);
        return Result<EmergencyContactDto>.Success(mapper.ToDto(contact));
    }
}
