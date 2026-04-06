using Beacon.Application.Common.Models;

namespace Beacon.Application.Common.Interfaces.IService
{
    public interface ICurrentUserService
    {
        CurrentUserInfo GetCurrentUser();
        int? GetUserId();
        string? GetUserName();
        string? GetRole();
        bool IsAuthenticated();
        bool IsInRole(string role);
    }
}
