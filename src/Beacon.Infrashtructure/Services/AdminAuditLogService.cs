using System.Text.Json;
using System.Text.Json.Nodes;
using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.AdminAuditLogs.Dtos;
using Beacon.Application.Features.Management.Statistics.Dtos;
using Beacon.Domain.Entities.Identity;
using Beacon.Infrashtructure.Presistence;
using Beacon.Shared.Common.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Services;

public class AdminAuditLogService(AppDbContext db) : IAdminAuditLogService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public async Task<string?> CaptureOldDataAsync(
        string controller,
        IReadOnlyDictionary<string, object?> routeValues,
        CancellationToken ct = default)
    {
        var id = ResolveEntityId(routeValues);
        if (!id.HasValue)
            return null;

        object? snapshot = controller switch
        {
            "AdminAccounts" => await db.Admins
                .AsNoTracking()
                .Where(x => x.Id == id.Value)
                .Select(x => new
                {
                    x.Id,
                    x.Username,
                    x.FullName,
                    x.IsActive,
                    x.LastLoginAtUtc,
                    RoleIds = x.AdminRoles.Select(ar => ar.RoleId).ToList()
                })
                .FirstOrDefaultAsync(ct),

            "UserAccounts" => await db.Users
                .AsNoTracking()
                .Where(x => x.Id == id.Value)
                .Select(x => new
                {
                    x.Id,
                    x.Username,
                    x.Email,
                    x.FamilyName,
                    x.GivenName,
                    x.PhoneNumber,
                    x.TimeZone,
                    x.IsActive,
                    x.IsEmailVerified,
                    x.AvatarMediaObjectId,
                    x.LastLoginAtUtc,
                    x.LastActiveAtUtc
                })
                .FirstOrDefaultAsync(ct),

            "Roles" => await db.Roles
                .AsNoTracking()
                .Where(x => x.Id == id.Value)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Description,
                    x.IsActive,
                    PermissionIds = x.RolePermissions.Select(rp => rp.PermissionId).ToList()
                })
                .FirstOrDefaultAsync(ct),

            "Permissions" => await db.Permissions
                .AsNoTracking()
                .Where(x => x.Id == id.Value)
                .Select(x => new { x.Id, x.Name, x.Description, x.Group })
                .FirstOrDefaultAsync(ct),

            "ManagedPosts" => await db.Posts
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => x.Id == id.Value)
                .Select(x => new
                {
                    x.Id,
                    x.OwnerUserId,
                    x.MediaId,
                    x.Caption,
                    x.Visibility,
                    x.Status,
                    x.DeletedAtUtc,
                    x.DeletedReason
                })
                .FirstOrDefaultAsync(ct),

            "PostReports" => await db.PostReports
                .AsNoTracking()
                .Where(x => x.Id == id.Value)
                .Select(x => new
                {
                    x.Id,
                    x.PostId,
                    x.ReporterUserId,
                    x.Reason,
                    x.Description,
                    x.Status,
                    x.ReviewedByAdminId,
                    x.ReviewedAtUtc,
                    x.ResolutionNote
                })
                .FirstOrDefaultAsync(ct),

            _ => null
        };

        return snapshot is null ? null : Serialize(snapshot);
    }

    public async Task WriteAsync(AdminAuditLogWriteRequest request, CancellationToken ct = default)
    {
        var log = AdminAuditLog.Create(
            request.AdminId,
            request.AdminUsername,
            request.HttpMethod,
            request.Path,
            request.QueryString,
            request.Controller,
            request.Action,
            request.EntityName,
            request.EntityId,
            SanitizeJson(request.RequestJson),
            SanitizeJson(request.OldDataJson),
            SanitizeJson(request.NewDataJson),
            SanitizeJson(request.ResponseJson),
            request.StatusCode,
            request.IsSuccess,
            request.CanRollback,
            request.IpAddress,
            request.UserAgent);

        await db.AdminAuditLogs.AddAsync(log, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<PaginatedList<AdminAuditLogDto>> ListAsync(
        int page,
        int pageSize,
        Guid? adminId,
        string? entityName,
        Guid? entityId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default)
    {
        var query = db.AdminAuditLogs.AsNoTracking().AsQueryable();

        if (adminId.HasValue)
            query = query.Where(x => x.AdminId == adminId.Value);

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            var keyword = entityName.Trim().ToLowerInvariant();
            query = query.Where(x => x.EntityName != null && x.EntityName.ToLower().Contains(keyword));
        }

        if (entityId.HasValue)
            query = query.Where(x => x.EntityId == entityId.Value);

        if (fromUtc.HasValue)
            query = query.Where(x => x.CreatedAtUtc >= fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(x => x.CreatedAtUtc <= toUtc.Value);

        query = query.OrderByDescending(x => x.CreatedAtUtc);
        var paged = await PaginatedList<AdminAuditLog>.CreateAsync(query, page, pageSize, ct);

        return new PaginatedList<AdminAuditLogDto>(
            paged.Items.Select(ToDto).ToList(),
            paged.TotalCount,
            paged.Page,
            paged.PageSize);
    }

    public async Task<AdminAuditLogDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var log = await db.AdminAuditLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return log is null ? null : ToDto(log);
    }

    public async Task<IReadOnlyList<AdminActivityStatisticItemDto>> GetAdminActivityStatisticsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        var rows = await db.AdminAuditLogs
            .AsNoTracking()
            .Where(x => x.AdminId != null
                        && x.CreatedAtUtc >= fromUtc
                        && x.CreatedAtUtc <= toUtc)
            .GroupBy(x => new { x.AdminId, x.AdminUsername })
            .Select(g => new
            {
                AdminId = g.Key.AdminId,
                g.Key.AdminUsername,
                ActionCount = g.Count(),
                LastActivityAtUtc = g.Max(x => x.CreatedAtUtc)
            })
            .ToListAsync(ct);

        return rows
            .Where(x => x.AdminId.HasValue)
            .Select(x => new AdminActivityStatisticItemDto(
                x.AdminId!.Value,
                x.AdminUsername,
                x.ActionCount,
                x.LastActivityAtUtc))
            .OrderByDescending(x => x.ActionCount)
            .ThenByDescending(x => x.LastActivityAtUtc)
            .ToList();
    }

    private static Guid? ResolveEntityId(IReadOnlyDictionary<string, object?> routeValues)
    {
        foreach (var key in new[] { "id", "roleId", "permissionId", "postId" })
        {
            if (routeValues.TryGetValue(key, out var value)
                && Guid.TryParse(Convert.ToString(value), out var id))
                return id;
        }

        return null;
    }

    private static string Serialize(object value)
        => JsonSerializer.Serialize(value, JsonOptions);

    private static string? SanitizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return json;

        try
        {
            var node = JsonNode.Parse(json);
            if (node is null) return json;

            RemoveSensitiveFields(node);
            return node.ToJsonString(JsonOptions);
        }
        catch
        {
            return json;
        }
    }

    private static void RemoveSensitiveFields(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(x => x.Key).ToList())
            {
                if (IsSensitive(key))
                {
                    obj.Remove(key);
                    continue;
                }

                if (obj[key] is JsonNode child)
                    RemoveSensitiveFields(child);
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var child in arr.OfType<JsonNode>())
                RemoveSensitiveFields(child);
        }
    }

    private static bool IsSensitive(string key)
        => key.Contains("password", StringComparison.OrdinalIgnoreCase)
           || key.Contains("token", StringComparison.OrdinalIgnoreCase)
           || key.Contains("secret", StringComparison.OrdinalIgnoreCase);

    private static AdminAuditLogDto ToDto(AdminAuditLog log) => new()
    {
        Id = log.Id,
        AdminId = log.AdminId,
        AdminUsername = log.AdminUsername,
        HttpMethod = log.HttpMethod,
        Path = log.Path,
        QueryString = log.QueryString,
        Controller = log.Controller,
        Action = log.Action,
        EntityName = log.EntityName,
        EntityId = log.EntityId,
        RequestJson = log.RequestJson,
        OldDataJson = log.OldDataJson,
        NewDataJson = log.NewDataJson,
        ResponseJson = log.ResponseJson,
        StatusCode = log.StatusCode,
        IsSuccess = log.IsSuccess,
        CanRollback = log.CanRollback,
        IpAddress = log.IpAddress,
        UserAgent = log.UserAgent,
        CreatedAtUtc = log.CreatedAtUtc
    };
}
