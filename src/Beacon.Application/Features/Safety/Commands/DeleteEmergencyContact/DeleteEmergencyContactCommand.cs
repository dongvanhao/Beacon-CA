using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Safety.Commands.DeleteEmergencyContact;

public record DeleteEmergencyContactCommand(Guid UserId, Guid ContactId)
    : IRequest<Result<bool>>;
