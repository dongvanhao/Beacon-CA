using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Infrashtructure.Presistence;

namespace Beacon.Infrashtructure.Repository.Messaging
{
    public class MessageGroupMemberSettingRepository(AppDbContext db) : IMessageGroupMemberSettingRepository
    {
        public async Task AddAsync(MessageGroupMemberSetting setting, CancellationToken ct)
            => await db.MessageGroupMemberSettings.AddAsync(setting, ct);

        public Task SaveChangesAsync(CancellationToken ct)
            => db.SaveChangesAsync(ct);
    }
}
