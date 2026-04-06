using Beacon.Application.Features.Health.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers
{
    [ApiController]
    [Route("api/health")]
    public class HealthController : BaseController
    {
        private readonly CheckDatabaseHealthUseCase _checkDatabaseHealthUseCase;

        public HealthController(CheckDatabaseHealthUseCase checkDatabaseHealthUseCase)
        {
            _checkDatabaseHealthUseCase = checkDatabaseHealthUseCase;
        }

        [HttpGet("database")]
        public async Task<IActionResult> CheckDatabaseAsync(CancellationToken cancellationToken)
        {
            var result = await _checkDatabaseHealthUseCase.CheckAsync(cancellationToken);
            return HandleResult(result, "Database health check passed");
        }
    }
}