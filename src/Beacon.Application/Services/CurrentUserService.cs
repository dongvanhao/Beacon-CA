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

        public Guid DeviceId =>
            Guid.TryParse(_user?.FindFirst("device_id")?.Value, out var deviceId)
                ? deviceId
                : Guid.Empty;

        public string Username =>
            _user?.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;

        public string FamilyName =>
            _user?.FindFirst(ClaimTypes.Surname)?.Value ?? string.Empty;

        public string GivenName =>
            _user?.FindFirst(ClaimTypes.GivenName)?.Value ?? string.Empty;

        public bool IsAuthenticated =>
            _user?.Identity?.IsAuthenticated ?? false;
    }
}