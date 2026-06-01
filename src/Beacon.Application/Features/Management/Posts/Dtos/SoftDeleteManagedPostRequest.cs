namespace Beacon.Application.Features.Management.Posts.Dtos;

public record SoftDeleteManagedPostRequest
{
    public string? Reason { get; init; }
}
