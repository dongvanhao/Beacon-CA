using Beacon.Application.Features.Identity.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public record UpdateCurrentAdminCommand(Guid AdminId, UpdateCurrentAdminRequest Request)
    : IRequest<Result<AdminProfileDto>>;
