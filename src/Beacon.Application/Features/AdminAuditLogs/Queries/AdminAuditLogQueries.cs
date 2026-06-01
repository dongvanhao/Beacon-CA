using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.AdminAuditLogs.Dtos;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.AdminAuditLogs.Queries;

public record ListAdminAuditLogsQuery(
    int Page,
    int PageSize,
    Guid? AdminId,
    string? EntityName,
    Guid? EntityId,
    DateTime? FromUtc,
    DateTime? ToUtc) : IRequest<Result<PaginatedList<AdminAuditLogDto>>>;

public record GetAdminAuditLogByIdQuery(Guid Id) : IRequest<Result<AdminAuditLogDto>>;

public class ListAdminAuditLogsQueryHandler(IAdminAuditLogService auditLogService)
    : IRequestHandler<ListAdminAuditLogsQuery, Result<PaginatedList<AdminAuditLogDto>>>
{
    public async Task<Result<PaginatedList<AdminAuditLogDto>>> Handle(ListAdminAuditLogsQuery query, CancellationToken ct)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var logs = await auditLogService.ListAsync(
            page,
            pageSize,
            query.AdminId,
            query.EntityName,
            query.EntityId,
            query.FromUtc,
            query.ToUtc,
            ct);

        return Result<PaginatedList<AdminAuditLogDto>>.Success(logs);
    }
}

public class GetAdminAuditLogByIdQueryHandler(IAdminAuditLogService auditLogService)
    : IRequestHandler<GetAdminAuditLogByIdQuery, Result<AdminAuditLogDto>>
{
    public async Task<Result<AdminAuditLogDto>> Handle(GetAdminAuditLogByIdQuery query, CancellationToken ct)
    {
        var log = await auditLogService.GetByIdAsync(query.Id, ct);
        return log is null
            ? Result<AdminAuditLogDto>.Failure(Error.NotFound(ErrorCodes.AdminAuditLog.LOG_NOT_FOUND, "Admin audit log khong ton tai."))
            : Result<AdminAuditLogDto>.Success(log);
    }
}
