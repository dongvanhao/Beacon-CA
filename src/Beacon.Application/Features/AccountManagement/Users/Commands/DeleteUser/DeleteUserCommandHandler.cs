using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.AccountManagement.Users.Commands.DeleteUser;

public class DeleteUserCommandHandler(IUserRepository userRepository)
    : IRequestHandler<DeleteUserCommand, Result>
{
    public async Task<Result> Handle(DeleteUserCommand command, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(command.UserId, ct);
        if (user is null)
            return Result.Failure(
                Error.NotFound(ErrorCodes.Identity.USER_NOT_FOUND, "Khong tim thay user."));

        user.Deactivate();
        await userRepository.SaveChangesAsync(ct);

        return Result.Success();
    }
}
