using Beacon.Domain.Entities.Group;
using Beacon.Domain.IRepository.Group;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Group;

public class NotificationRepository(AppDbContext db) : INotificationRepository
{
    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<List<Notification>> ListByReceiverAsync(
        Guid receiverUserId, DateTime? cursor, int limit, CancellationToken ct = default)
    {
        var query = db.Notifications
            .AsNoTracking()
            .Where(n => n.ReceiverUserId == receiverUserId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .AsQueryable();

        if (cursor.HasValue)
            query = query.Where(n => n.CreatedAtUtc < cursor.Value);

        return await query.Take(limit).ToListAsync(ct);
    }

    public Task<int> CountUnreadAsync(Guid receiverUserId, CancellationToken ct = default)
        => db.Notifications.CountAsync(n => n.ReceiverUserId == receiverUserId && !n.IsRead, ct);

    public async Task AddAsync(Notification notification, CancellationToken ct = default)
        => await db.Notifications.AddAsync(notification, ct);

    public async Task MarkAllReadAsync(Guid receiverUserId, CancellationToken ct = default)
    {
        var unread = await db.Notifications
            .Where(n => n.ReceiverUserId == receiverUserId && !n.IsRead)
            .ToListAsync(ct);

        if (unread.Count == 0) return;

        foreach (var n in unread)
            n.MarkRead();

        await db.SaveChangesAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
