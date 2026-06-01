using Beacon.Application.Features.AccountManagement.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.AccountManagement.Admins.Commands.CreateAdmin;

public record CreateAdminCommand(CreateAdminAccountRequest Request) : IRequest<Result<AdminAccountDto>>;
