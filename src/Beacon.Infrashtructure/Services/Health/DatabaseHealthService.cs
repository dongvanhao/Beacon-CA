using Beacon.Application.Common.Interfaces.IService;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Services.Health
{
    public class DatabaseHealthService : IDatabaseHealthService
    {
        private static readonly TimeSpan DatabaseCheckTimeout = TimeSpan.FromSeconds(5);
        private readonly AppDbContext _dbContext;

        public DatabaseHealthService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<bool> IsDatabaseReachableAsync(CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(DatabaseCheckTimeout);

            try
            {
                return await _dbContext.Database.CanConnectAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Database connectivity check timed out.");
            }
        }
    }
}
