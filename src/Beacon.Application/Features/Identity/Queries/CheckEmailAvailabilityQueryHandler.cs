using Beacon.Application.Features.Identity.Dtos;
using Beacon.Domain.IRepository;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Queries;

public class CheckEmailAvailabilityQueryHandler(IUserRepository userRepository)
    : IRequestHandler<CheckEmailAvailabilityQuery, Result<AvailabilityResponse>>
{
    public async Task<Result<AvailabilityResponse>> Handle(CheckEmailAvailabilityQuery query, CancellationToken ct)
    {
        var exists = await userRepository.ExistsByEmailAsync(query.Email, ct);
        return Result<AvailabilityResponse>.Success(new AvailabilityResponse { Available = !exists });
    }
}
