using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Application.Mappings.Identity;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public class RefreshAdminTokenCommandHandler(
    IAdminRepository adminRepository,
    IJwtService jwtService,
    AdminAuthMapper adminAuthMapper) : IRequestHandler<RefreshAdminTokenCommand, Result<AdminAuthResponse>>
{
    public async Task<Result<AdminAuthResponse>> Handle(RefreshAdminTokenCommand command, CancellationToken ct)
    {
        var existingToken = await adminRepository.GetActiveRefreshTokenAsync(command.RefreshToken, ct);
        if (existingToken is null)
            return Result<AdminAuthResponse>.Failure(
                Error.Unauthorized(ErrorCodes.Identity.ADMIN_TOKEN_INVALID, "Refresh token is invalid or expired."));

        var admin = await adminRepository.GetByIdWithRolesAsync(existingToken.AdminId, ct);
        if (admin is null)
            return Result<AdminAuthResponse>.Failure(
                Error.NotFound(ErrorCodes.Identity.ADMIN_NOT_FOUND, "Admin not found."));

        if (!admin.IsActive)
            return Result<AdminAuthResponse>.Failure(
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

        var (accessToken, accessTokenExpiresAt) = jwtService.GenerateAdminAccessToken(admin, activeRoles, permissions);
        var (newRefreshTokenValue, newRefreshTokenExpiresAt) = jwtService.GenerateRefreshToken();

        existingToken.Revoke(newRefreshTokenValue);

        var newRefreshToken = RefreshTokenAdmin.Create(
            adminId: admin.Id,
            token: newRefreshTokenValue,
            expiresAtUtc: newRefreshTokenExpiresAt);

        await adminRepository.AddRefreshTokenAsync(newRefreshToken, ct);
        await adminRepository.SaveChangesAsync(ct);

        return Result<AdminAuthResponse>.Success(
            adminAuthMapper.ToAuthResponse(admin, accessToken, newRefreshTokenValue, accessTokenExpiresAt, permissions));
    }
}
