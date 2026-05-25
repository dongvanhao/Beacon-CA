using Beacon.Application.Features.Safety.Dtos;
using Beacon.Application.Mappings.Safety;
using Beacon.Domain.IRepository.Safety;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Safety.Queries.GetEmergencyContacts;

public class GetEmergencyContactsQueryHandler(
    IEmergencyContactRepository repo,
    EmergencyContactMapper mapper)
    : IRequestHandler<GetEmergencyContactsQuery, Result<IReadOnlyList<EmergencyContactDto>>>
{
    public async Task<Result<IReadOnlyList<EmergencyContactDto>>> Handle(
        GetEmergencyContactsQuery query, CancellationToken ct)
    {
        var contacts = await repo.GetByUserIdAsync(query.UserId, ct);
        return Result<IReadOnlyList<EmergencyContactDto>>.Success(
            contacts.Select(mapper.ToDto).ToList());
    }
}
