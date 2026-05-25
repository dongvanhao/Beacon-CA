using Beacon.Application.Features.Safety.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Safety.Commands.SetPrimaryEmergencyContact;

public record SetPrimaryEmergencyContactCommand(Guid UserId, Guid ContactId)
    : IRequest<Result<EmergencyContactDto>>;
