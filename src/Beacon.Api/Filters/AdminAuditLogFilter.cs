using System.Security.Claims;
using System.Text.Json;
using Beacon.Application.Common.Interfaces.IService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Beacon.Api.Filters;

public class AdminAuditLogFilter(IAdminAuditLogService auditLogService, ILogger<AdminAuditLogFilter> logger)
    : IAsyncActionFilter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!ShouldAudit(context))
        {
            await next();
            return;
        }

        var descriptor = (ControllerActionDescriptor)context.ActionDescriptor;
        var controller = descriptor.ControllerName;
        var action = descriptor.ActionName;
        var routeValues = context.RouteData.Values.ToDictionary(x => x.Key, x => x.Value);
        var oldDataJson = await SafeCaptureOldDataAsync(controller, action, routeValues, context.HttpContext.RequestAborted);
        var requestJson = SafeSerialize(BuildRequestPayload(context), controller, action);

        ActionExecutedContext? executed = null;
        Exception? exception = null;

        try
        {
            executed = await next();
            exception = executed.Exception;
        }
        finally
        {
            try
            {
                var result = executed?.Result;
                var responseJson = SerializeResult(result);
                var newDataJson = ExtractDataJson(result);
                var statusCode = ResolveStatusCode(result, context.HttpContext.Response.StatusCode);
                var isSuccess = exception is null && statusCode is >= 200 and < 300;
                var method = context.HttpContext.Request.Method;

                await auditLogService.WriteAsync(new AdminAuditLogWriteRequest(
                    ResolveAdminId(context.HttpContext.User),
                    context.HttpContext.User.FindFirst(ClaimTypes.Name)?.Value,
                    method,
                    context.HttpContext.Request.Path.Value ?? string.Empty,
                    context.HttpContext.Request.QueryString.HasValue ? context.HttpContext.Request.QueryString.Value : null,
                    controller,
                    action,
                    ResolveEntityName(controller),
                    ResolveEntityId(routeValues, newDataJson),
                    requestJson,
                    oldDataJson,
                    newDataJson,
                    responseJson,
                    statusCode,
                    isSuccess,
                    isSuccess && oldDataJson is not null && IsMutatingMethod(method),
                    context.HttpContext.Connection.RemoteIpAddress?.ToString(),
                    context.HttpContext.Request.Headers.UserAgent.ToString()),
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Admin audit logging failed for {Controller}.{Action}", controller, action);
            }
        }
    }

    private static bool ShouldAudit(ActionExecutingContext context)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
            return false;

        if (HttpMethods.IsGet(context.HttpContext.Request.Method))
            return false;

        var ns = descriptor.ControllerTypeInfo.Namespace ?? string.Empty;
        return ns.Contains(".Controllers.Management", StringComparison.Ordinal)
               || ns.Contains(".Controllers.Authorization", StringComparison.Ordinal);
    }

    private static Guid? ResolveAdminId(ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    private static string? ResolveEntityName(string controller)
        => controller switch
        {
            "AdminAccounts" => "Admin",
            "UserAccounts" => "User",
            "Roles" => "Role",
            "Permissions" => "Permission",
            "ManagedPosts" => "Post",
            "PostReports" => "PostReport",
            "Statistics" => "Statistics",
            "AdminAuditLogs" => "AdminAuditLog",
            "SuperAdminPermissions" => "PermissionCatalog",
            _ => controller
        };

    private static Guid? ResolveEntityId(IReadOnlyDictionary<string, object?> routeValues, string? newDataJson)
    {
        foreach (var key in new[] { "id", "roleId", "permissionId", "postId" })
        {
            if (routeValues.TryGetValue(key, out var value)
                && Guid.TryParse(Convert.ToString(value), out var id))
                return id;
        }

        if (string.IsNullOrWhiteSpace(newDataJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(newDataJson);
            return doc.RootElement.TryGetProperty("id", out var idProp)
                   && Guid.TryParse(idProp.GetString(), out var id)
                ? id
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsMutatingMethod(string method)
        => HttpMethods.IsPost(method)
           || HttpMethods.IsPut(method)
           || HttpMethods.IsPatch(method)
           || HttpMethods.IsDelete(method);

    private static Dictionary<string, object?> BuildRequestPayload(ActionExecutingContext context)
        => context.ActionArguments
            .Where(x => x.Value is not CancellationToken)
            .ToDictionary(x => x.Key, x => x.Value);

    private async Task<string?> SafeCaptureOldDataAsync(
        string controller,
        string action,
        IReadOnlyDictionary<string, object?> routeValues,
        CancellationToken ct)
    {
        try
        {
            return await auditLogService.CaptureOldDataAsync(controller, routeValues, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Admin audit old-data capture failed for {Controller}.{Action}", controller, action);
            return null;
        }
    }

    private string? SafeSerialize(object? value, string controller, string action)
    {
        try
        {
            return Serialize(value);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Admin audit request serialization failed for {Controller}.{Action}", controller, action);
            return null;
        }
    }

    private static int? ResolveStatusCode(IActionResult? result, int fallback)
        => result switch
        {
            ObjectResult objectResult => objectResult.StatusCode ?? fallback,
            StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
            _ => fallback == 0 ? null : fallback
        };

    private static string? Serialize(object? value)
        => value is null ? null : JsonSerializer.Serialize(value, JsonOptions);

    private static string? SerializeResult(IActionResult? result)
        => result switch
        {
            ObjectResult objectResult => Serialize(objectResult.Value),
            JsonResult jsonResult => Serialize(jsonResult.Value),
            _ => null
        };

    private static string? ExtractDataJson(IActionResult? result)
    {
        var responseJson = SerializeResult(result);
        if (string.IsNullOrWhiteSpace(responseJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            return doc.RootElement.TryGetProperty("data", out var data)
                ? data.GetRawText()
                : responseJson;
        }
        catch
        {
            return responseJson;
        }
    }
}
