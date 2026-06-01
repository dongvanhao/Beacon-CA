using Beacon.Application.Features.AccountManagement.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.AccountManagement.Users.Commands.UpdateUser;

public record UpdateUserCommand(Guid UserId, UpdateUserAccountRequest Request) : IRequest<Result<UserAccountDto>>;
