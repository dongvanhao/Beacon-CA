using Beacon.Application.Features.Identity.Dtos;
using Beacon.Application.Mappings.Identity;
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Queries;

public class GetCurrentAdminQueryHandler(
    IAdminRepository adminRepository,
    AdminAuthMapper adminAuthMapper) : IRequestHandler<GetCurrentAdminQuery, Result<AdminProfileDto>>
{
    public async Task<Result<AdminProfileDto>> Handle(GetCurrentAdminQuery query, CancellationToken ct)
    {
        var admin = await adminRepository.GetByIdWithRolesAsync(query.AdminId, ct);
        if (admin is null)
            return Result<AdminProfileDto>.Failure(
                Error.NotFound(ErrorCodes.Identity.ADMIN_NOT_FOUND, "Admin not found."));

        if (!admin.IsActive)
            return Result<AdminProfileDto>.Failure(
                Error.Unauthorized(ErrorCodes.Identity.ADMIN_INACTIVE, "Admin account is inactive."));

        var activeRoles = admin.AdminRoles
            .Where(ar => ar.Role.IsActive)
            .Select(ar => ar.Role.Name)
            .Distinct()
            .ToList();

        var permissions = admin.AdminRoles
            .Where(ar => ar.Role.IsActive)
            .SelectMany(ar => ar.Role.RolePermissions)
            .Select(rp => rp.Permission.Name)
            .Distinct()
            .ToList();

        return Result<AdminProfileDto>.Success(
            adminAuthMapper.ToProfileDto(admin, activeRoles, permissions));
    }
}
