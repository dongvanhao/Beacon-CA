using Beacon.Application.Features.Group.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Queries.SearchUsers;

public record SearchUsersQuery(string Search, int Limit = 10) : IRequest<Result<List<UserSearchDto>>>;
