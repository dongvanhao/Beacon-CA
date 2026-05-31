using Beacon.Application.Features.Identity.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Queries;

public record GetCurrentAdminQuery(Guid AdminId) : IRequest<Result<AdminProfileDto>>;
