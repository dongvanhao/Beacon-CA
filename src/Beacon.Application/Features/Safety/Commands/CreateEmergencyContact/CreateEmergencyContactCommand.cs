using Beacon.Application.Features.Safety.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Safety.Commands.CreateEmergencyContact;

public record CreateEmergencyContactCommand(Guid UserId, CreateEmergencyContactRequest Request)
    : IRequest<Result<EmergencyContactDto>>;
