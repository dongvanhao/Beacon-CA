using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Infrashtructure.Presistence;
using Beacon.Shared.Common.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Messaging
{
    public class MessageRepository(AppDbContext db) : IMessageRepository
    {
        public async Task<CursorPagedResult<Message>> ListByGroupAsync(
            Guid groupId, DateTime? cursor, int limit, CancellationToken ct)
        {
            var query = db.Messages
                .AsNoTracking()
                .Include(m => m.Sender)
                .Where(m => m.GroupId == groupId)
                .OrderByDescending(m => m.CreatedAtUtc)
                .AsQueryable();

            if (cursor.HasValue)
                query = query.Where(m => m.CreatedAtUtc < cursor.Value);

            var items = await query.Take(limit + 1).ToListAsync(ct);
            var hasMore = items.Count > limit;
            if (hasMore) items.RemoveAt(items.Count - 1);

            return new CursorPagedResult<Message>
            {
                Data = items,
                Meta = new CursorMeta
                {
                    NextCursor = hasMore ? items[^1].CreatedAtUtc : null,
                    Limit = limit,
                    HasMore = hasMore
                }
            };
        }

        public async Task AddAsync(Message message, CancellationToken ct)
            => await db.Messages.AddAsync(message, ct);

        public Task SaveChangesAsync(CancellationToken ct)
            => db.SaveChangesAsync(ct);
    }
}
