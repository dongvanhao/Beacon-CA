namespace Beacon.Application.Common.Interfaces.IService
{
    public interface IDatabaseHealthService
    {
        Task<bool> IsDatabaseReachableAsync(CancellationToken cancellationToken);
    }
}
