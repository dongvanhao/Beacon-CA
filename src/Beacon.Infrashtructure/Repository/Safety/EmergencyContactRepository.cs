using Beacon.Domain.Entities.Safety;
using Beacon.Domain.IRepository.Safety;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Safety;

public class EmergencyContactRepository(AppDbContext db) : IEmergencyContactRepository
{
    public async Task<IReadOnlyList<EmergencyContact>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await db.EmergencyContacts
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.PriorityOrder)
            .ToListAsync(ct);

    public Task<EmergencyContact?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.EmergencyContacts.FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<EmergencyContact?> GetPrimaryByUserIdAsync(Guid userId, CancellationToken ct = default)
        => db.EmergencyContacts.FirstOrDefaultAsync(e => e.UserId == userId && e.IsPrimary, ct);

    public Task<int> CountActiveByUserIdAsync(Guid userId, CancellationToken ct = default)
        => db.EmergencyContacts.CountAsync(e => e.UserId == userId && e.IsActive, ct);

    public async Task AddAsync(EmergencyContact contact, CancellationToken ct = default)
        => await db.EmergencyContacts.AddAsync(contact, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
