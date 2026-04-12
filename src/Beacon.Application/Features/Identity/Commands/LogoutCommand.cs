using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public record LogoutCommand(string RefreshToken) : IRequest<Result>;
