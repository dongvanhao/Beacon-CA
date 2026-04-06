namespace Beacon.Domain.Entities.User
{
    public class User
    {
        public int Id { get; set; }
        public string UserName { get; set; } = default!;
        public string PasswordHash { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public int? AvatarMediaId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Media? AvatarMedia { get; set; }
        public UserSetting? UserSetting { get; set; }
        public ICollection<UserRefreshToken> RefreshTokens { get; set; } = new List<UserRefreshToken>();
        public ICollection<Media> UploadedMedia { get; set; } = new List<Media>();
    }
}
