using Beacon.Application.Features.AccountManagement.Dtos;
using Beacon.Application.Mappings.AccountManagement;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Identity;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.AccountManagement.Admins.Commands.UpdateAdmin;

public class UpdateAdminCommandHandler(
    IAdminRepository adminRepository,
    IRoleRepository roleRepository,
    AccountManagementMapper mapper)
    : IRequestHandler<UpdateAdminCommand, Result<AdminAccountDto>>
{
    public async Task<Result<AdminAccountDto>> Handle(UpdateAdminCommand command, CancellationToken ct)
    {
        var admin = await adminRepository.GetByIdWithRolesAsync(command.AdminId, ct);
        if (admin is null)
            return Result<AdminAccountDto>.Failure(
                Error.NotFound(ErrorCodes.Identity.ADMIN_NOT_FOUND, "Khong tim thay admin."));

        var request = command.Request;
        var username = request.Username.Trim().ToLowerInvariant();
        if (await adminRepository.ExistsByUsernameAsync(username, admin.Id, ct))
            return Result<AdminAccountDto>.Failure(
                Error.Conflict(ErrorCodes.Identity.USERNAME_ALREADY_EXISTS, "Username da ton tai."));

        var roleIds = request.RoleIds?.Distinct().ToArray();
        if (roleIds is not null)
        {
            var roleValidation = await ValidateRolesAsync(roleIds, ct);
            if (roleValidation.IsFailure)
                return Result<AdminAccountDto>.Failure(roleValidation.Error);
        }

        admin.Update(username, request.FullName);
        if (!string.IsNullOrWhiteSpace(request.Password))
            admin.UpdatePassword(BCrypt.Net.BCrypt.HashPassword(request.Password));

        if (request.IsActive)
            admin.Activate();
        else
            admin.Deactivate();

        if (roleIds is not null)
            await ReplaceRolesAsync(admin.Id, roleIds, ct);

        await adminRepository.SaveChangesAsync(ct);

        var updatedAdmin = await adminRepository.GetByIdWithRolesNoTrackingAsync(admin.Id, ct);
        return Result<AdminAccountDto>.Success(mapper.ToDto(updatedAdmin!));
    }

    private async Task<Result> ValidateRolesAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken ct)
    {
        if (roleIds.Count == 0)
            return Result.Success();

        var roles = await roleRepository.GetByIdsAsync(roleIds, ct);
        if (roles.Count != roleIds.Count)
            return Result.Failure(Error.NotFound(ErrorCodes.Authorization.ROLE_NOT_FOUND, "Khong tim thay role."));

        if (roles.Any(r => !r.IsActive))
            return Result.Failure(Error.Conflict(ErrorCodes.Authorization.ROLE_INACTIVE, "Role da bi vo hieu hoa."));

        return Result.Success();
    }

    private async Task ReplaceRolesAsync(Guid adminId, IReadOnlyCollection<Guid> roleIds, CancellationToken ct)
    {
        var currentRoles = await roleRepository.ListAdminRolesByAdminIdAsync(adminId, ct);
        var nextRoleIds = roleIds.ToHashSet();
        var currentRoleIds = currentRoles.Select(ar => ar.RoleId).ToHashSet();

        foreach (var currentRole in currentRoles.Where(ar => !nextRoleIds.Contains(ar.RoleId)))
        {
            roleRepository.RemoveAdminRole(currentRole);
        }

        foreach (var roleId in nextRoleIds.Where(id => !currentRoleIds.Contains(id)))
        {
            await roleRepository.AddAdminRoleAsync(AdminRole.Create(adminId, roleId), ct);
        }
    }
}
