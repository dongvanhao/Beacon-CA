using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Health.Dtos;
using Beacon.Shared.Common;
using Beacon.Shared.Results;

namespace Beacon.Application.Features.Health.UseCases
{
    public class CheckDatabaseHealthUseCase
    {
        private readonly IDatabaseHealthService _databaseHealthService;

        public CheckDatabaseHealthUseCase(IDatabaseHealthService databaseHealthService)
        {
            _databaseHealthService = databaseHealthService;
        }

        public async Task<Result<DatabaseHealthDto>> CheckAsync(CancellationToken cancellationToken)
        {
            var canConnect = await _databaseHealthService.IsDatabaseReachableAsync(cancellationToken);

            if (!canConnect)
            {
                return Result<DatabaseHealthDto>.Failure(
                    new Error(ErrorCodes.DatabaseUnavailable, "Cannot connect to database."));
            }

            return Result<DatabaseHealthDto>.Success(
                new DatabaseHealthDto(true, "Database connection is healthy."));
        }
    }
}
