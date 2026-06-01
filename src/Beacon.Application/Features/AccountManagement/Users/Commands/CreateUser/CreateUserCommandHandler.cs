using Beacon.Application.Features.AccountManagement.Dtos;
using Beacon.Application.Mappings.AccountManagement;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.AccountManagement.Users.Commands.CreateUser;

public class CreateUserCommandHandler(
    IUserRepository userRepository,
    AccountManagementMapper mapper)
    : IRequestHandler<CreateUserCommand, Result<UserAccountDto>>
{
    public async Task<Result<UserAccountDto>> Handle(CreateUserCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var username = request.Username.Trim().ToLowerInvariant();
        var email = request.Email.Trim().ToLowerInvariant();
        var phone = NormalizeNullable(request.PhoneNumber);

        if (await userRepository.ExistsByUsernameAsync(username, ct: ct))
            return Result<UserAccountDto>.Failure(
                Error.Conflict(ErrorCodes.Identity.USERNAME_ALREADY_EXISTS, "Username da ton tai."));

        if (await userRepository.ExistsByEmailAsync(email, ct))
            return Result<UserAccountDto>.Failure(
                Error.Conflict(ErrorCodes.Identity.EMAIL_ALREADY_EXISTS, "Email da ton tai."));

        if (!string.IsNullOrWhiteSpace(phone)
            && await userRepository.ExistsByPhoneAsync(phone, ct))
            return Result<UserAccountDto>.Failure(
                Error.Conflict(ErrorCodes.Identity.PHONE_ALREADY_EXISTS, "So dien thoai da ton tai."));

        var user = User.Create(
            username,
            email,
            BCrypt.Net.BCrypt.HashPassword(request.Password),
            request.FamilyName.Trim(),
            request.GivenName.Trim(),
            phone,
            request.TimeZone.Trim(),
            request.IsEmailVerified);

        if (request.IsActive)
            user.Activate();
        else
            user.Deactivate();

        await userRepository.AddAsync(user, ct);
        await userRepository.SaveChangesAsync(ct);

        return Result<UserAccountDto>.Success(mapper.ToDto(user));
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
