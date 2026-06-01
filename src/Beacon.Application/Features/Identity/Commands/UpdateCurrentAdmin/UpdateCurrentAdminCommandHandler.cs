using Beacon.Application.Features.Identity.Dtos;
using Beacon.Application.Mappings.Identity;
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public class UpdateCurrentAdminCommandHandler(
    IAdminRepository adminRepository,
    AdminAuthMapper adminAuthMapper)
    : IRequestHandler<UpdateCurrentAdminCommand, Result<AdminProfileDto>>
{
    public async Task<Result<AdminProfileDto>> Handle(UpdateCurrentAdminCommand command, CancellationToken ct)
    {
        var admin = await adminRepository.GetByIdWithRolesAsync(command.AdminId, ct);
        if (admin is null)
            return Result<AdminProfileDto>.Failure(
                Error.NotFound(ErrorCodes.Identity.ADMIN_NOT_FOUND, "Admin not found."));

        if (!admin.IsActive)
            return Result<AdminProfileDto>.Failure(
                Error.Unauthorized(ErrorCodes.Identity.ADMIN_INACTIVE, "Admin account is inactive."));

        var username = string.IsNullOrWhiteSpace(command.Request.Username)
            ? admin.Username
            : command.Request.Username.Trim().ToLowerInvariant();
        var fullName = string.IsNullOrWhiteSpace(command.Request.FullName)
            ? admin.FullName
            : command.Request.FullName.Trim();

        if (username != admin.Username
            && await adminRepository.ExistsByUsernameAsync(username, admin.Id, ct))
            return Result<AdminProfileDto>.Failure(
                Error.Conflict(ErrorCodes.Identity.USERNAME_ALREADY_EXISTS, "Username da ton tai."));

        admin.Update(username, fullName);
        await adminRepository.SaveChangesAsync(ct);

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
