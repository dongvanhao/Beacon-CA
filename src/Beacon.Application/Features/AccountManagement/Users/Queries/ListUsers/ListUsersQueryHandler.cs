using Beacon.Application.Features.AccountManagement.Dtos;
using Beacon.Application.Mappings.AccountManagement;
using Beacon.Domain.IRepository;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.AccountManagement.Users.Queries.ListUsers;

public class ListUsersQueryHandler(
    IUserRepository userRepository,
    AccountManagementMapper mapper)
    : IRequestHandler<ListUsersQuery, Result<PaginatedList<UserAccountDto>>>
{
    public async Task<Result<PaginatedList<UserAccountDto>>> Handle(ListUsersQuery query, CancellationToken ct)
    {
        var users = await userRepository.ListAsync(query.Search, query.Page, query.PageSize, ct);
        var items = users.Items.Select(mapper.ToDto).ToList();

        return Result<PaginatedList<UserAccountDto>>.Success(new PaginatedList<UserAccountDto>(
            items,
            users.TotalCount,
            users.Page,
            users.PageSize));
    }
}
