namespace Beacon.Domain.Entities.User
{
    public class Media
    {
        public int Id { get; set; }
        public string Bucket { get; set; } = default!;
        public string ObjectKey { get; set; } = default!;
        public string? FileName { get; set; }
        public string? Type { get; set; }
        public string? MimeType { get; set; }
        public int? Size { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public int? Duration { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }

        public User? Creator { get; set; }
        public ICollection<User> AvatarUsers { get; set; } = new List<User>();
    }
}
