using Beacon.Application.Features.AccountManagement.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.AccountManagement.Admins.Commands.UpdateAdmin;

public record UpdateAdminCommand(Guid AdminId, UpdateAdminAccountRequest Request) : IRequest<Result<AdminAccountDto>>;
