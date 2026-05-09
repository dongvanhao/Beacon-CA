using Beacon.Domain.Entities.Messaging;

namespace Beacon.Domain.IRepository.Messaging
{
    public interface IMessageGroupMemberSettingRepository
    {
        Task AddAsync(MessageGroupMemberSetting setting, CancellationToken ct);
        Task SaveChangesAsync(CancellationToken ct);
    }
}
