using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public class LoginAdminCommandHandler(
    IAdminRepository adminRepository,
    IJwtService jwtService) : IRequestHandler<LoginAdminCommand, Result<AdminAuthResponse>>
{
    public async Task<Result<AdminAuthResponse>> Handle(LoginAdminCommand command, CancellationToken ct)
    {
        var req = command.Request;

        // 1. Tìm Admin kèm đầy đủ Roles → Permissions
        var admin = await adminRepository.GetByEmailWithRolesAsync(req.Email, ct);
        if (admin is null)
            return Result<AdminAuthResponse>.Failure(
                Error.Unauthorized(ErrorCodes.Identity.INVALID_CREDENTIALS, "Invalid email or password."));

        // 2. Kiểm tra trạng thái tài khoản
        if (!admin.IsActive)
            return Result<AdminAuthResponse>.Failure(
                Error.Unauthorized(ErrorCodes.Identity.ADMIN_INACTIVE, "Admin account is inactive."));

        // 3. Xác thực mật khẩu
        if (!BCrypt.Net.BCrypt.Verify(req.Password, admin.PasswordHash))
            return Result<AdminAuthResponse>.Failure(
                Error.Unauthorized(ErrorCodes.Identity.INVALID_CREDENTIALS, "Invalid email or password."));

        // 4. Ghi nhận thời điểm đăng nhập
        admin.RecordLogin();

        // 5. Thu thập tất cả permissions từ tất cả roles đang active
        var permissions = admin.AdminRoles
            .Where(ar => ar.Role.IsActive)
            .SelectMany(ar => ar.Role.RolePermissions)
            .Select(rp => rp.Permission.Name)
            .Distinct()
            .ToList();

        // 6. Sinh tokens
        var (accessToken, accessTokenExpiresAt) = jwtService.GenerateAdminAccessToken(admin, permissions);
        var (refreshTokenValue, refreshTokenExpiresAt) = jwtService.GenerateRefreshToken();
        var refreshTokenEntity = RefreshTokenAdmin.Create(
            adminId: admin.Id,
            token: refreshTokenValue,
            expiresAtUtc: refreshTokenExpiresAt);

        await adminRepository.AddRefreshTokenAsync(refreshTokenEntity, ct);
        await adminRepository.SaveChangesAsync(ct);

        return Result<AdminAuthResponse>.Success(new AdminAuthResponse
        {
            AdminId = admin.Id,
            Email = admin.Email,
            FullName = admin.FullName,
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            AccessTokenExpiresAt = accessTokenExpiresAt,
            Permissions = permissions
        });
    }
}
