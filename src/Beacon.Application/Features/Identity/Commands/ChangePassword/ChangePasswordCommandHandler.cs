using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands.ChangePassword;

public class ChangePasswordCommandHandler(
    IUserRepository userRepository,
    ICurrentUserService currentUser)
    : IRequestHandler<ChangePasswordCommand, Result>
{
    public async Task<Result> Handle(ChangePasswordCommand command, CancellationToken ct)
    {
        var userId = currentUser.UserId;

        var user = await userRepository.GetByIdAsync(userId, ct);
        if (user is null)
            return Result.Failure(Error.Unauthorized(ErrorCodes.Identity.TOKEN_INVALID, "Token không hợp lệ."));

        if (!user.IsActive)
            return Result.Failure(Error.Unauthorized(ErrorCodes.Identity.ACCOUNT_INACTIVE, "Tài khoản đã bị vô hiệu hóa."));

        if (!BCrypt.Net.BCrypt.Verify(command.Request.CurrentPassword, user.PasswordHash))
            return Result.Failure(Error.Unauthorized(ErrorCodes.Identity.INVALID_CURRENT_PASSWORD, "Mật khẩu hiện tại không đúng."));

        if (command.Request.NewPassword == command.Request.CurrentPassword)
            return Result.Failure(Error.Validation(ErrorCodes.Identity.NEW_PASSWORD_SAME_AS_OLD, "Mật khẩu mới phải khác mật khẩu hiện tại."));

        user.UpdatePassword(BCrypt.Net.BCrypt.HashPassword(command.Request.NewPassword));

        var activeTokens = await userRepository.GetActiveRefreshTokensByUserIdAsync(userId, ct);
        foreach (var token in activeTokens)
            token.Revoke();

        await userRepository.SaveChangesAsync(ct);
        return Result.Success();
    }
}
