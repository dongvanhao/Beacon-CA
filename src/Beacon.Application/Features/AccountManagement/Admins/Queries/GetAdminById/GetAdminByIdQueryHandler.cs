using Beacon.Application.Features.AccountManagement.Dtos;
using Beacon.Application.Mappings.AccountManagement;
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.AccountManagement.Admins.Queries.GetAdminById;

public class GetAdminByIdQueryHandler(
    IAdminRepository adminRepository,
    AccountManagementMapper mapper)
    : IRequestHandler<GetAdminByIdQuery, Result<AdminAccountDto>>
{
    public async Task<Result<AdminAccountDto>> Handle(GetAdminByIdQuery query, CancellationToken ct)
    {
        var admin = await adminRepository.GetByIdWithRolesNoTrackingAsync(query.AdminId, ct);
        if (admin is null)
            return Result<AdminAccountDto>.Failure(
                Error.NotFound(ErrorCodes.Identity.ADMIN_NOT_FOUND, "Khong tim thay admin."));

        return Result<AdminAccountDto>.Success(mapper.ToDto(admin));
    }
}
