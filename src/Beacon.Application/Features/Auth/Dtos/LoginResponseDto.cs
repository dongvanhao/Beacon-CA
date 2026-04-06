namespace Beacon.Application.Features.Auth.Dtos
{
    public record LoginResponseDto(
        string AccessToken,
        string TokenType,
        int ExpiresInSeconds,
        string UserName,
        int UserId,
        string Role);
}
