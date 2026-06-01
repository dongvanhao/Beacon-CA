using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.AccountManagement.Admins.Commands.DeleteAdmin;

public record DeleteAdminCommand(Guid AdminId) : IRequest<Result>;
