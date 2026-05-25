using Beacon.Application.Features.Safety.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Safety.Commands.UpdateEmergencyContact;

public record UpdateEmergencyContactCommand(Guid UserId, Guid ContactId, UpdateEmergencyContactRequest Request)
    : IRequest<Result<EmergencyContactDto>>;
