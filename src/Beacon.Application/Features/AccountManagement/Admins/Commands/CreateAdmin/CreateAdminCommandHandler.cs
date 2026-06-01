using Beacon.Application.Features.AccountManagement.Dtos;
using Beacon.Application.Mappings.AccountManagement;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Identity;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.AccountManagement.Admins.Commands.CreateAdmin;

public class CreateAdminCommandHandler(
    IAdminRepository adminRepository,
    IRoleRepository roleRepository,
    AccountManagementMapper mapper)
    : IRequestHandler<CreateAdminCommand, Result<AdminAccountDto>>
{
    public async Task<Result<AdminAccountDto>> Handle(CreateAdminCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var username = request.Username.Trim().ToLowerInvariant();

        if (await adminRepository.ExistsByUsernameAsync(username, ct: ct))
            return Result<AdminAccountDto>.Failure(
                Error.Conflict(ErrorCodes.Identity.USERNAME_ALREADY_EXISTS, "Username da ton tai."));

        var roleIds = (request.RoleIds ?? []).Distinct().ToArray();
        var roleValidation = await ValidateRolesAsync(roleIds, ct);
        if (roleValidation.IsFailure)
            return Result<AdminAccountDto>.Failure(roleValidation.Error);

        var admin = Admin.Create(
            username,
            BCrypt.Net.BCrypt.HashPassword(request.Password),
            request.FullName);

        await adminRepository.AddAsync(admin, ct);
        foreach (var roleId in roleIds)
        {
            await roleRepository.AddAdminRoleAsync(AdminRole.Create(admin.Id, roleId), ct);
        }

        await adminRepository.SaveChangesAsync(ct);

        var createdAdmin = await adminRepository.GetByIdWithRolesNoTrackingAsync(admin.Id, ct);
        return Result<AdminAccountDto>.Success(mapper.ToDto(createdAdmin!));
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
}
