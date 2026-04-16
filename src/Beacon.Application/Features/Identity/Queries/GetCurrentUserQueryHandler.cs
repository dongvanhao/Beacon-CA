using Beacon.Application.Features.Identity.Dtos;
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Queries;

public class GetCurrentUserQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetCurrentUserQuery, Result<UserProfileDto>>
{
    public async Task<Result<UserProfileDto>> Handle(GetCurrentUserQuery query, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(query.UserId, ct);
        if (user is null)
            return Result<UserProfileDto>.Failure(
                Error.NotFound(ErrorCodes.Identity.USER_NOT_FOUND, "User not found."));

        return Result<UserProfileDto>.Success(new UserProfileDto
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            TimeZone = user.TimeZone,
            IsActive = user.IsActive,
            IsEmailVerified = user.IsEmailVerified,
            LastLoginAtUtc = user.LastLoginAtUtc,
            CreatedAtUtc = user.CreatedAtUtc
        });
    }
}
