using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Identity;

public class UserDeviceRepository(AppDbContext context) : IUserDeviceRepository
{
    public async Task<UserDevice?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.UserDevices.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<UserDevice?> GetByDeviceTokenAsync(Guid userId, string deviceToken, CancellationToken ct = default)
        => await context.UserDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceToken == deviceToken, ct);

    public async Task AddAsync(UserDevice device, CancellationToken ct = default)
        => await context.UserDevices.AddAsync(device, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
