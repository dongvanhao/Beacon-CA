using Beacon.Application.Features.Safety.Dtos;
using Beacon.Application.Mappings.Safety;
using Beacon.Domain.Entities.Safety;
using Beacon.Domain.IRepository.Safety;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Safety.Commands.CreateEmergencyContact;

public class CreateEmergencyContactCommandHandler(
    IEmergencyContactRepository repo,
    EmergencyContactMapper mapper)
    : IRequestHandler<CreateEmergencyContactCommand, Result<EmergencyContactDto>>
{
    public async Task<Result<EmergencyContactDto>> Handle(
        CreateEmergencyContactCommand cmd, CancellationToken ct)
    {
        var count = await repo.CountActiveByUserIdAsync(cmd.UserId, ct);
        if (count >= 5)
            return Result<EmergencyContactDto>.Failure(
                Error.Conflict(ErrorCodes.Safety.EMERGENCY_CONTACT_LIMIT_EXCEEDED,
                    "Bạn chỉ có thể thêm tối đa 5 liên hệ khẩn cấp."));

        var contact = EmergencyContact.Create(
            cmd.UserId,
            cmd.Request.FullName,
            cmd.Request.ContactValue,
            cmd.Request.ChannelType,
            cmd.Request.Relationship,
            cmd.Request.PriorityOrder);

        await repo.AddAsync(contact, ct);
        await repo.SaveChangesAsync(ct);
        return Result<EmergencyContactDto>.Success(mapper.ToDto(contact));
    }
}
