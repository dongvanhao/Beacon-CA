using Beacon.Application.Features.AccountManagement.Dtos;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.AccountManagement.Users.Queries.ListUsers;

public record ListUsersQuery(int Page = 1, int PageSize = 20, string? Search = null)
    : IRequest<Result<PaginatedList<UserAccountDto>>>;
