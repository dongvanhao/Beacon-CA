using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Application.Mappings.Authorization;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Identity;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Roles.Commands.AssignRoleToAdmin;

public class AssignRoleToAdminCommandHandler(
    IRoleRepository roleRepository,
    IAdminRepository adminRepository,
    RoleMapper mapper) : IRequestHandler<AssignRoleToAdminCommand, Result<AdminRoleAssignmentDto>>
{
    public async Task<Result<AdminRoleAssignmentDto>> Handle(AssignRoleToAdminCommand command, CancellationToken ct)
    {
        var role = await roleRepository.GetByIdAsync(command.RoleId, ct);
        if (role is null)
            return Result<AdminRoleAssignmentDto>.Failure(
                Error.NotFound(ErrorCodes.Authorization.ROLE_NOT_FOUND, "Không tìm thấy role."));

        if (!role.IsActive)
            return Result<AdminRoleAssignmentDto>.Failure(
                Error.Conflict(ErrorCodes.Authorization.ROLE_INACTIVE, "Role đã bị vô hiệu hóa."));

        var admin = await adminRepository.GetByIdAsync(command.AdminId, ct);
        if (admin is null)
            return Result<AdminRoleAssignmentDto>.Failure(
                Error.NotFound(ErrorCodes.Identity.ADMIN_NOT_FOUND, "Không tìm thấy admin."));

        if (!admin.IsActive)
            return Result<AdminRoleAssignmentDto>.Failure(
                Error.Unauthorized(ErrorCodes.Identity.ADMIN_INACTIVE, "Admin account is inactive."));

        if (await roleRepository.HasAdminRoleAsync(admin.Id, role.Id, ct))
            return Result<AdminRoleAssignmentDto>.Failure(
                Error.Conflict(ErrorCodes.Authorization.ADMIN_ROLE_ALREADY_EXISTS, "Admin đã có role này."));

        var adminRole = AdminRole.Create(admin.Id, role.Id);
        await roleRepository.AddAdminRoleAsync(adminRole, ct);
        await roleRepository.SaveChangesAsync(ct);

        return Result<AdminRoleAssignmentDto>.Success(
            mapper.ToAdminRoleAssignmentDto(admin, role, adminRole));
    }
}
