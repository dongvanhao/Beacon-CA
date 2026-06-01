using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.AccountManagement.Users.Commands.DeleteUser;

public record DeleteUserCommand(Guid UserId) : IRequest<Result>;
