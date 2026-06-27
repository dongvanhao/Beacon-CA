using Serilog;

namespace Beacon.Api.Logging;


/// <summary>
/// Extension methods cấu hình Serilog cho Beacon.Api.
/// Console-only sink (stdout) — file/Seq/Loki/OTel cần ADR riêng.
/// </summary>
public static class SerilogConfiguration
{
    /// <summary>
    /// Tạo bootstrap logger sớm nhất trong <c>Program.cs</c> (trước <c>builder.Build()</c>)
    /// để bắt lỗi giai đoạn startup/DI. Sau đó reconfigure từ <c>IConfiguration</c>.
    /// </summary>
    public static void CreateBootstrapLogger() =>
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

    /// <summary>
    /// Thay logging provider mặc định bằng Serilog: đọc config từ <c>appsettings.json</c>,
    /// lấy service từ DI, và bật <c>FromLogContext</c>.
    /// </summary>
    public static WebApplicationBuilder AddSerilogLogging(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((ctx, services, cfg) => cfg
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());

        return builder;
    }

    /// <summary>
    /// Bật <c>UseSerilogRequestLogging</c>: một dòng summary mỗi request.
    /// Level qua <see cref="RequestLoggingHelper.ResolveLevel"/>; field bổ sung
    /// (<c>UserId</c>/<c>ClientIp</c>/<c>QueryStringSanitized</c>) qua helper thuần.
    /// </summary>
    public static WebApplication UseRequestLogging(this WebApplication app, bool maskIp)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = (httpCtx, _, ex) =>
                RequestLoggingHelper.ResolveLevel(
                    httpCtx.Response.StatusCode,
                    RequestLoggingHelper.IsExcluded(httpCtx.Request.Path),
                    ex is not null);

            options.EnrichDiagnosticContext = (diag, httpCtx) =>
            {
                diag.Set("UserId", RequestLoggingHelper.ResolveUserId(httpCtx.User));
                diag.Set("ClientIp", RequestLoggingHelper.MaskIp(httpCtx.Connection.RemoteIpAddress, maskIp));

                if (httpCtx.Request.QueryString.HasValue)
                    diag.Set("QueryStringSanitized",
                        RequestLoggingHelper.SanitizeQueryString(httpCtx.Request.Query));
            };
        });

        return app;
    }
}
