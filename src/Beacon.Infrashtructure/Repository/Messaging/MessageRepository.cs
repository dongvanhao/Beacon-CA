using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Infrashtructure.Presistence;
using Beacon.Shared.Common.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Messaging
{
    public class MessageRepository(AppDbContext db) : IMessageRepository
    {
        public async Task<CursorPagedResult<Message, long>> ListByGroupAsync(
            Guid groupId, long? cursor, int limit, CancellationToken ct)
        {
            var query = db.Messages
                .AsNoTracking()
                .Include(m => m.Sender)
                .Where(m => m.GroupId == groupId)
                .Where(m => cursor == null || m.SequenceNumber < cursor)
                .OrderByDescending(m => m.SequenceNumber)
                .AsQueryable();

            var items = await query.Take(limit + 1).ToListAsync(ct);
            var hasMore = items.Count > limit;
            if (hasMore) items.RemoveAt(items.Count - 1);

            return new CursorPagedResult<Message, long>
            {
                Data = items,
                Meta = new CursorMeta<long>
                {
                    NextCursor = hasMore ? items[^1].SequenceNumber : (long?)null,
                    Limit = limit,
                    HasMore = hasMore
                }
            };
        }

        public Task<Message?> GetByClientMessageIdAsync(Guid groupId, string clientMessageId, CancellationToken ct)
            => db.Messages
                .FirstOrDefaultAsync(m => m.GroupId == groupId && m.ClientMessageId == clientMessageId, ct);

        public Task<bool> ExistsInGroupAsync(Guid groupId, Guid messageId, CancellationToken ct)
            => db.Messages.AnyAsync(m => m.Id == messageId && m.GroupId == groupId, ct);

        public async Task AddAsync(Message message, CancellationToken ct)
            => await db.Messages.AddAsync(message, ct);

        public Task SaveChangesAsync(CancellationToken ct)
            => db.SaveChangesAsync(ct);
    }
}
