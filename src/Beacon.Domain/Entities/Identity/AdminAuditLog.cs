using Beacon.Domain.Common;

namespace Beacon.Domain.Entities.Identity;

public class AdminAuditLog : AuditableEntity
{
    public Guid? AdminId { get; private set; }
    public string? AdminUsername { get; private set; }
    public string HttpMethod { get; private set; } = default!;
    public string Path { get; private set; } = default!;
    public string? QueryString { get; private set; }
    public string Controller { get; private set; } = default!;
    public string Action { get; private set; } = default!;
    public string? EntityName { get; private set; }
    public Guid? EntityId { get; private set; }
    public string? RequestJson { get; private set; }
    public string? OldDataJson { get; private set; }
    public string? NewDataJson { get; private set; }
    public string? ResponseJson { get; private set; }
    public int? StatusCode { get; private set; }
    public bool IsSuccess { get; private set; }
    public bool CanRollback { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    protected AdminAuditLog() { }

    public static AdminAuditLog Create(
        Guid? adminId,
        string? adminUsername,
        string httpMethod,
        string path,
        string? queryString,
        string controller,
        string action,
        string? entityName,
        Guid? entityId,
        string? requestJson,
        string? oldDataJson,
        string? newDataJson,
        string? responseJson,
        int? statusCode,
        bool isSuccess,
        bool canRollback,
        string? ipAddress,
        string? userAgent)
        => new()
        {
            AdminId = adminId,
            AdminUsername = adminUsername,
            HttpMethod = httpMethod,
            Path = path,
            QueryString = queryString,
            Controller = controller,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            RequestJson = requestJson,
            OldDataJson = oldDataJson,
            NewDataJson = newDataJson,
            ResponseJson = responseJson,
            StatusCode = statusCode,
            IsSuccess = isSuccess,
            CanRollback = canRollback,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };
}
