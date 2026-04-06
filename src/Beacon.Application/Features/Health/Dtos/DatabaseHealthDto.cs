namespace Beacon.Application.Features.Health.Dtos
{
    public record DatabaseHealthDto(bool IsConnected, string Description);
}
