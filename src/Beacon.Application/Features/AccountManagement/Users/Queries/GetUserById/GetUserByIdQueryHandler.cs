using Beacon.Application.Features.AccountManagement.Dtos;
using Beacon.Application.Mappings.AccountManagement;
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.AccountManagement.Users.Queries.GetUserById;

public class GetUserByIdQueryHandler(
    IUserRepository userRepository,
    AccountManagementMapper mapper)
    : IRequestHandler<GetUserByIdQuery, Result<UserAccountDto>>
{
    public async Task<Result<UserAccountDto>> Handle(GetUserByIdQuery query, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(query.UserId, ct);
        if (user is null)
            return Result<UserAccountDto>.Failure(
                Error.NotFound(ErrorCodes.Identity.USER_NOT_FOUND, "Khong tim thay user."));

        return Result<UserAccountDto>.Success(mapper.ToDto(user));
    }
}
