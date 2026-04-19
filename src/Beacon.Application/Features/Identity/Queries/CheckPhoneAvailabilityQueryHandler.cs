using Beacon.Application.Features.Identity.Dtos;
using Beacon.Domain.IRepository;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Queries;

public class CheckPhoneAvailabilityQueryHandler(IUserRepository userRepository)
    : IRequestHandler<CheckPhoneAvailabilityQuery, Result<AvailabilityResponse>>
{
    public async Task<Result<AvailabilityResponse>> Handle(CheckPhoneAvailabilityQuery query, CancellationToken ct)
    {
        var exists = await userRepository.ExistsByPhoneAsync(query.PhoneNumber, ct);
        return Result<AvailabilityResponse>.Success(new AvailabilityResponse { Available = !exists });
    }
}
