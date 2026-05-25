using Beacon.Application.Features.Safety.Dtos;
using Beacon.Application.Mappings.Safety;
using Beacon.Domain.IRepository.Safety;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Safety.Commands.UpdateEmergencyContact;

public class UpdateEmergencyContactCommandHandler(
    IEmergencyContactRepository repo,
    EmergencyContactMapper mapper)
    : IRequestHandler<UpdateEmergencyContactCommand, Result<EmergencyContactDto>>
{
    public async Task<Result<EmergencyContactDto>> Handle(
        UpdateEmergencyContactCommand cmd, CancellationToken ct)
    {
        var contact = await repo.GetByIdAsync(cmd.ContactId, ct);
        if (contact is null)
            return Result<EmergencyContactDto>.Failure(
                Error.NotFound(ErrorCodes.Safety.EMERGENCY_CONTACT_NOT_FOUND,
                    "Không tìm thấy liên hệ khẩn cấp."));

        if (contact.UserId != cmd.UserId)
            return Result<EmergencyContactDto>.Failure(
                Error.Forbidden(ErrorCodes.Safety.EMERGENCY_CONTACT_FORBIDDEN,
                    "Bạn không có quyền chỉnh sửa liên hệ này."));

        contact.Update(
            cmd.Request.FullName,
            cmd.Request.ContactValue,
            cmd.Request.ChannelType,
            cmd.Request.Relationship,
            cmd.Request.PriorityOrder);

        await repo.SaveChangesAsync(ct);
        return Result<EmergencyContactDto>.Success(mapper.ToDto(contact));
    }
}
