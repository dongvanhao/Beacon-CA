using Beacon.Api.Authorization;
using Beacon.Application.Features.AdminAuditLogs.Queries;
using Beacon.Shared.Constants;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers.Management;

[Route("api/v1/admin/audit-logs")]
public class AdminAuditLogsController(IMediator mediator) : BaseController
{
    /// <summary>
    /// Lay danh sach log thao tac admin.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>GET /api/v1/admin/audit-logs</c>
    ///
    /// <b>Auth:</b> Yeu cau Admin access token va permission <c>admin-audit-logs:read</c>.
    ///
    /// <b>Query params:</b>
    /// - <c>page</c>, <c>pageSize</c>: Pagination.
    /// - <c>adminId</c>: Loc theo admin.
    /// - <c>entityName</c>: Loc theo doi tuong, vi du <c>User</c>, <c>Role</c>, <c>Post</c>.
    /// - <c>entityId</c>: Loc theo id doi tuong.
    /// - <c>fromUtc</c>, <c>toUtc</c>: Khoang thoi gian UTC.
    ///
    /// <b>Response 200:</b> Tra ve danh sach log co <c>oldDataJson</c>, <c>newDataJson</c>,
    /// <c>requestJson</c>, <c>responseJson</c> va <c>canRollback</c>.
    /// </remarks>
    [HttpGet]
    [AdminOnly]
    [HasPermission(PermissionCodes.AdminAuditLogs.Read)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? adminId = null,
        [FromQuery] string? entityName = null,
        [FromQuery] Guid? entityId = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken ct = default)
        => HandleResult(await mediator.Send(
            new ListAdminAuditLogsQuery(page, pageSize, adminId, entityName, entityId, fromUtc, toUtc), ct));

    /// <summary>
    /// Lay chi tiet mot log thao tac admin.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>GET /api/v1/admin/audit-logs/{id}</c>
    ///
    /// <b>Auth:</b> Yeu cau Admin access token va permission <c>admin-audit-logs:read</c>.
    ///
    /// <b>Response 200:</b> Tra ve chi tiet log, bao gom data cu/data moi va co the rollback hay khong.
    ///
    /// <b>Error codes:</b> <c>ADMIN_AUDIT_LOG_NOT_FOUND</c>, <c>UNAUTHORIZED</c>, <c>FORBIDDEN</c>.
    /// </remarks>
    [HttpGet("{id:guid}")]
    [AdminOnly]
    [HasPermission(PermissionCodes.AdminAuditLogs.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(new GetAdminAuditLogByIdQuery(id), ct));
}
