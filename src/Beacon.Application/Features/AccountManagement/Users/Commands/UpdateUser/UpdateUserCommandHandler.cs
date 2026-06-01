using Beacon.Application.Features.AccountManagement.Dtos;
using Beacon.Application.Mappings.AccountManagement;
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.AccountManagement.Users.Commands.UpdateUser;

public class UpdateUserCommandHandler(
    IUserRepository userRepository,
    AccountManagementMapper mapper)
    : IRequestHandler<UpdateUserCommand, Result<UserAccountDto>>
{
    public async Task<Result<UserAccountDto>> Handle(UpdateUserCommand command, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(command.UserId, ct);
        if (user is null)
            return Result<UserAccountDto>.Failure(
                Error.NotFound(ErrorCodes.Identity.USER_NOT_FOUND, "Khong tim thay user."));

        var request = command.Request;
        var username = request.Username.Trim().ToLowerInvariant();
        var email = request.Email.Trim().ToLowerInvariant();
        var phone = NormalizeNullable(request.PhoneNumber);

        if (await userRepository.ExistsByUsernameAsync(username, user.Id, ct))
            return Result<UserAccountDto>.Failure(
                Error.Conflict(ErrorCodes.Identity.USERNAME_ALREADY_EXISTS, "Username da ton tai."));

        if (await userRepository.ExistsByEmailExcludingUserAsync(email, user.Id, ct))
            return Result<UserAccountDto>.Failure(
                Error.Conflict(ErrorCodes.Identity.EMAIL_ALREADY_IN_USE, "Email da duoc su dung."));

        if (!string.IsNullOrWhiteSpace(phone)
            && await userRepository.ExistsByPhoneExcludingUserAsync(phone, user.Id, ct))
            return Result<UserAccountDto>.Failure(
                Error.Conflict(ErrorCodes.Identity.PHONE_ALREADY_IN_USE, "So dien thoai da duoc su dung."));

        user.UpdateManagedProfile(
            username,
            email,
            request.FamilyName,
            request.GivenName,
            phone,
            request.TimeZone,
            request.IsEmailVerified);

        if (!string.IsNullOrWhiteSpace(request.Password))
            user.UpdatePassword(BCrypt.Net.BCrypt.HashPassword(request.Password));

        if (request.IsActive)
            user.Activate();
        else
            user.Deactivate();

        await userRepository.SaveChangesAsync(ct);

        return Result<UserAccountDto>.Success(mapper.ToDto(user));
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
