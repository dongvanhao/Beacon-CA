namespace Beacon.Domain.Entities.Messaging
{
    public class MessageGroupMemberSetting
    {
        public Guid GroupId { get; private set; }
        public Guid UserId { get; private set; }
        public string? CustomName { get; private set; }
        public bool IsMuted { get; private set; }
        public Guid? LastReadMessageId { get; private set; }
        public DateTime? LastReadAtUtc { get; private set; }

        private MessageGroupMemberSetting() { }

        public static MessageGroupMemberSetting Create(Guid groupId, Guid userId)
            => new() { GroupId = groupId, UserId = userId };

        public void UpdateCustomName(string? name) => CustomName = name;
        public void SetMuted(bool muted) => IsMuted = muted;

        public void MarkRead(Guid messageId, DateTime readAtUtc)
        {
            LastReadMessageId = messageId;
            LastReadAtUtc = readAtUtc;
        }
    }
}
