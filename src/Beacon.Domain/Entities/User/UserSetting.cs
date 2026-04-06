namespace Beacon.Domain.Entities.User
{
    public class UserSetting
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Theme { get; set; } = "light";
        public bool NotifyMessage { get; set; } = true;
        public bool NotifyHealth { get; set; } = true;
        public bool HealthReminderEnabled { get; set; } = false;
        public TimeSpan? HealthReminderTime { get; set; }
        public int? HealthInactiveThreshold { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public User User { get; set; } = default!;
    }
}
