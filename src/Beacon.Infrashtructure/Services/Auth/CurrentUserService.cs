using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Common.Models;
using Microsoft.AspNetCore.Http;

namespace Beacon.Infrashtructure.Services.Auth
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public CurrentUserInfo GetCurrentUser()
        {
            var principal = _httpContextAccessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return new CurrentUserInfo
                {
                    IsAuthenticated = false
                };
            }

            var userIdValue = GetClaimValue(principal, ClaimTypes.NameIdentifier)
                ?? GetClaimValue(principal, JwtRegisteredClaimNames.Sub);

            int? userId = null;
            if (int.TryParse(userIdValue, out var parsedUserId))
            {
                userId = parsedUserId;
            }

            var userName = GetClaimValue(principal, ClaimTypes.Name)
                ?? GetClaimValue(principal, JwtRegisteredClaimNames.UniqueName);

            var role = GetClaimValue(principal, ClaimTypes.Role);

            return new CurrentUserInfo
            {
                IsAuthenticated = true,
                UserId = userId,
                UserName = userName,
                Role = role
            };
        }

        public int? GetUserId() => GetCurrentUser().UserId;

        public string? GetUserName() => GetCurrentUser().UserName;

        public string? GetRole() => GetCurrentUser().Role;

        public bool IsAuthenticated() => GetCurrentUser().IsAuthenticated;

        public bool IsInRole(string role)
        {
            var principal = _httpContextAccessor.HttpContext?.User;
            return principal?.IsInRole(role) ?? false;
        }

        private static string? GetClaimValue(ClaimsPrincipal principal, string claimType)
        {
            return principal.FindFirst(claimType)?.Value;
        }
    }
}
