using Beacon.Application.Features.AdminAuditLogs.Dtos;
using Beacon.Application.Features.Management.Statistics.Dtos;
using Beacon.Shared.Common.Pagination;

namespace Beacon.Application.Common.Interfaces.IService;

public interface IAdminAuditLogService
{
    Task<string?> CaptureOldDataAsync(string controller, IReadOnlyDictionary<string, object?> routeValues, CancellationToken ct = default);

    Task WriteAsync(AdminAuditLogWriteRequest request, CancellationToken ct = default);

    Task<PaginatedList<AdminAuditLogDto>> ListAsync(
        int page,
        int pageSize,
        Guid? adminId,
        string? entityName,
        Guid? entityId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default);

    Task<AdminAuditLogDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<AdminActivityStatisticItemDto>> GetAdminActivityStatisticsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default);
}

public record AdminAuditLogWriteRequest(
    Guid? AdminId,
    string? AdminUsername,
    string HttpMethod,
    string Path,
    string? QueryString,
    string Controller,
    string Action,
    string? EntityName,
    Guid? EntityId,
    string? RequestJson,
    string? OldDataJson,
    string? NewDataJson,
    string? ResponseJson,
    int? StatusCode,
    bool IsSuccess,
    bool CanRollback,
    string? IpAddress,
    string? UserAgent);
