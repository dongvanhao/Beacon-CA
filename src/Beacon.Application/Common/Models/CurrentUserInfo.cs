namespace Beacon.Application.Common.Models
{
    public class CurrentUserInfo
    {
        public bool IsAuthenticated { get; set; }
        public int? UserId { get; set; }
        public string? UserName { get; set; }
        public string? Role { get; set; }
    }
}
