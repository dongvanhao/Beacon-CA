using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Permissions.Queries.ListPermissionGroups;

public record ListPermissionGroupsQuery : IRequest<Result<IReadOnlyList<string>>>;
