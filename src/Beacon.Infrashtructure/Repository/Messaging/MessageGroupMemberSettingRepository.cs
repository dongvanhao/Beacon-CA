using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Messaging
{
    public class MessageGroupMemberSettingRepository(AppDbContext db) : IMessageGroupMemberSettingRepository
    {
        public async Task AddAsync(MessageGroupMemberSetting setting, CancellationToken ct)
            => await db.MessageGroupMemberSettings.AddAsync(setting, ct);

        public async Task AddIfNotExistsAsync(Guid groupId, Guid userId, CancellationToken ct)
        {
            var exists = await db.MessageGroupMemberSettings
                .AnyAsync(s => s.GroupId == groupId && s.UserId == userId, ct);
            if (!exists)
                await db.MessageGroupMemberSettings.AddAsync(
                    MessageGroupMemberSetting.Create(groupId, userId), ct);
        }

        public Task SaveChangesAsync(CancellationToken ct)
            => db.SaveChangesAsync(ct);
    }
}
