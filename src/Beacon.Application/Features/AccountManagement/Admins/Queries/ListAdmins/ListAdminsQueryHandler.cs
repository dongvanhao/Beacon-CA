using Beacon.Application.Features.AccountManagement.Dtos;
using Beacon.Application.Mappings.AccountManagement;
using Beacon.Domain.IRepository;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.AccountManagement.Admins.Queries.ListAdmins;

public class ListAdminsQueryHandler(
    IAdminRepository adminRepository,
    AccountManagementMapper mapper)
    : IRequestHandler<ListAdminsQuery, Result<PaginatedList<AdminAccountDto>>>
{
    public async Task<Result<PaginatedList<AdminAccountDto>>> Handle(ListAdminsQuery query, CancellationToken ct)
    {
        var admins = await adminRepository.ListAsync(query.Search, query.Page, query.PageSize, ct);
        var items = admins.Items.Select(mapper.ToDto).ToList();

        return Result<PaginatedList<AdminAccountDto>>.Success(new PaginatedList<AdminAccountDto>(
            items,
            admins.TotalCount,
            admins.Page,
            admins.PageSize));
    }
}
