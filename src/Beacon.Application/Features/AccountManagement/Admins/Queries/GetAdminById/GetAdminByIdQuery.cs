using Beacon.Application.Features.AccountManagement.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.AccountManagement.Admins.Queries.GetAdminById;

public record GetAdminByIdQuery(Guid AdminId) : IRequest<Result<AdminAccountDto>>;
