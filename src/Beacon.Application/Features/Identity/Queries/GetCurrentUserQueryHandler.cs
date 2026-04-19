using Beacon.Application.Features.Identity.Dtos;
using Beacon.Application.Mappings.Identity;
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Queries;

public class GetCurrentUserQueryHandler(
    IUserRepository userRepository,
    UserProfileMapper profileMapper)
    : IRequestHandler<GetCurrentUserQuery, Result<UserProfileDto>>
{
    public async Task<Result<UserProfileDto>> Handle(GetCurrentUserQuery query, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(query.UserId, ct);
        if (user is null)
            return Result<UserProfileDto>.Failure(
                Error.NotFound(ErrorCodes.Identity.USER_NOT_FOUND, "User not found."));

        return Result<UserProfileDto>.Success(profileMapper.ToProfileDto(user));
    }
}
