namespace Beacon.Application.Features.AdminAuditLogs.Dtos;

public record AdminAuditLogDto
{
    public Guid Id { get; init; }
    public Guid? AdminId { get; init; }
    public string? AdminUsername { get; init; }
    public string HttpMethod { get; init; } = default!;
    public string Path { get; init; } = default!;
    public string? QueryString { get; init; }
    public string Controller { get; init; } = default!;
    public string Action { get; init; } = default!;
    public string? EntityName { get; init; }
    public Guid? EntityId { get; init; }
    public string? RequestJson { get; init; }
    public string? OldDataJson { get; init; }
    public string? NewDataJson { get; init; }
    public string? ResponseJson { get; init; }
    public int? StatusCode { get; init; }
    public bool IsSuccess { get; init; }
    public bool CanRollback { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
