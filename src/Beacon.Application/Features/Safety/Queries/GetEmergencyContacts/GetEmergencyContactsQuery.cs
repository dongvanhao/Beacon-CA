using Beacon.Application.Features.Safety.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Safety.Queries.GetEmergencyContacts;

public record GetEmergencyContactsQuery(Guid UserId)
    : IRequest<Result<IReadOnlyList<EmergencyContactDto>>>;
