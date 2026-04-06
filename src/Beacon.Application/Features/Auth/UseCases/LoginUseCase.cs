using Beacon.Application.Common.Interfaces.IRepository;
using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Common.Options;
using Beacon.Application.Features.Auth.Dtos;
using Beacon.Shared.Common;
using Beacon.Shared.Results;
using Microsoft.Extensions.Options;

namespace Beacon.Application.Features.Auth.UseCases
{
    public class LoginUseCase
    {
        private readonly IAdminRepository _adminRepository;
        private readonly IUserRepository _userRepository;
        private readonly IJwtService _jwtService;
        private readonly IPasswordService _passwordService;
        private readonly JwtOptions _jwtOptions;

        public LoginUseCase(
            IAdminRepository adminRepository,
            IUserRepository userRepository,
            IJwtService jwtService,
            IPasswordService passwordService,
            IOptions<JwtOptions> jwtOptions)
        {
            _adminRepository = adminRepository;
            _userRepository = userRepository;
            _jwtService = jwtService;
            _passwordService = passwordService;
            _jwtOptions = jwtOptions.Value;
        }

        public async Task<Result<LoginResponseDto>> ExecuteAdminAsync(LoginRequestDto request, CancellationToken cancellationToken)
        {
            var validationError = ValidateRequest(request);
            if (validationError is not null)
            {
                return validationError;
            }

            return await LoginAdminAsync(request, cancellationToken);
        }

        public async Task<Result<LoginResponseDto>> ExecuteUserAsync(LoginRequestDto request, CancellationToken cancellationToken)
        {
            var validationError = ValidateRequest(request);
            if (validationError is not null)
            {
                return validationError;
            }

            return await LoginUserAsync(request, cancellationToken);
        }

        private async Task<Result<LoginResponseDto>> LoginAdminAsync(LoginRequestDto request, CancellationToken cancellationToken)
        {
            var admin = await _adminRepository.GetByUserNameAsync(request.UserName, cancellationToken);
            if (admin is null || !admin.IsActive)
            {
                return Result<LoginResponseDto>.Failure(
                    new Error(ErrorCodes.Unauthorized, "Invalid credentials."));
            }

            if (!_passwordService.Verify(request.Password, admin.PasswordHash))
            {
                return Result<LoginResponseDto>.Failure(
                    new Error(ErrorCodes.Unauthorized, "Invalid credentials."));
            }

            var accessToken = _jwtService.GenerateAccessTokenForAdmin(admin);

            return Result<LoginResponseDto>.Success(new LoginResponseDto(
                AccessToken: accessToken,
                TokenType: "Bearer",
                ExpiresInSeconds: _jwtOptions.AccessTokenExpirationMinutes * 60,
                UserName: admin.UserName,
                UserId: admin.Id,
                Role: SystemRoles.Admin));
        }

        private async Task<Result<LoginResponseDto>> LoginUserAsync(LoginRequestDto request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByUserNameAsync(request.UserName, cancellationToken);
            if (user is null)
            {
                return Result<LoginResponseDto>.Failure(
                    new Error(ErrorCodes.Unauthorized, "Invalid credentials."));
            }

            if (!_passwordService.Verify(request.Password, user.PasswordHash))
            {
                return Result<LoginResponseDto>.Failure(
                    new Error(ErrorCodes.Unauthorized, "Invalid credentials."));
            }

            var accessToken = _jwtService.GenerateAccessTokenForUser(user);

            return Result<LoginResponseDto>.Success(new LoginResponseDto(
                AccessToken: accessToken,
                TokenType: "Bearer",
                ExpiresInSeconds: _jwtOptions.AccessTokenExpirationMinutes * 60,
                UserName: user.UserName,
                UserId: user.Id,
                Role: SystemRoles.User));
        }

        private static Result<LoginResponseDto>? ValidateRequest(LoginRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Result<LoginResponseDto>.Failure(
                    new Error(ErrorCodes.ValidationError, "Username and password are required."));
            }

            return null;
        }
    }
}
