using Beacon.Application.Features.AccountManagement.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.AccountManagement.Users.Commands.CreateUser;

public record CreateUserCommand(CreateUserAccountRequest Request) : IRequest<Result<UserAccountDto>>;
