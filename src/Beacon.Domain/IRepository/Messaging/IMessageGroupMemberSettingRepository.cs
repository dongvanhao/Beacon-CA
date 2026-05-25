using Beacon.Domain.Entities.Messaging;

namespace Beacon.Domain.IRepository.Messaging
{
    public interface IMessageGroupMemberSettingRepository
    {
        Task<MessageGroupMemberSetting?> GetByGroupAndUserAsync(Guid groupId, Guid userId, CancellationToken ct);
        Task AddAsync(MessageGroupMemberSetting setting, CancellationToken ct);
        Task AddIfNotExistsAsync(Guid groupId, Guid userId, CancellationToken ct);
        Task SaveChangesAsync(CancellationToken ct);
    }
}
