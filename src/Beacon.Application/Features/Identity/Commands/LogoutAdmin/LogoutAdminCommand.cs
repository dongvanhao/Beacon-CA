using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public record LogoutAdminCommand(string RefreshToken) : IRequest<Result>;
