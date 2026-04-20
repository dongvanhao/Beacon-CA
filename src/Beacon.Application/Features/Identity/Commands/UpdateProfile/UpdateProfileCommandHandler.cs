using Beacon.Application.Features.Identity.Dtos;
using Beacon.Application.Mappings.Identity;
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands.UpdateProfile;

public class UpdateProfileCommandHandler(
    IUserRepository userRepository,
    UserProfileMapper mapper) : IRequestHandler<UpdateProfileCommand, Result<UserProfileDto>>
{
    public async Task<Result<UserProfileDto>> Handle(UpdateProfileCommand command, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(command.UserId, ct);
        if (user is null)
            return Result<UserProfileDto>.Failure(
                Error.NotFound(ErrorCodes.Identity.USER_NOT_FOUND, "Người dùng không tồn tại."));

        var req = command.Request;

        if (!string.IsNullOrWhiteSpace(req.PhoneNumber))
        {
            var trimmedPhone = req.PhoneNumber.Trim();
            if (trimmedPhone != user.PhoneNumber)
            {
                if (await userRepository.ExistsByPhoneExcludingUserAsync(trimmedPhone, command.UserId, ct))
                    return Result<UserProfileDto>.Failure(
                        Error.Conflict(ErrorCodes.Identity.PHONE_ALREADY_IN_USE, "Số điện thoại đã được sử dụng."));
            }
        }

        user.UpdateProfile(req.FamilyName, req.GivenName, req.PhoneNumber, req.TimeZone);
        await userRepository.SaveChangesAsync(ct);

        return Result<UserProfileDto>.Success(mapper.ToProfileDto(user));
    }
}
