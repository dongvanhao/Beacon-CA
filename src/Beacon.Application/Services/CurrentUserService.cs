using Beacon.Application.Common.Interfaces;
using Beacon.Application.Common.Interfaces.IService;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Beacon.Api.Services
{
    public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
    {
        private readonly ClaimsPrincipal? _user = httpContextAccessor.HttpContext?.User;

        public Guid UserId =>
            Guid.TryParse(_user?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
                ? id
                : Guid.Empty;

        public string Email =>
            _user?.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;

        public bool IsAuthenticated =>
            _user?.Identity?.IsAuthenticated ?? false;
    }
}