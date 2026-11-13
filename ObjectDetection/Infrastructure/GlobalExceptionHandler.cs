using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ObjectDetection.Infrastructure;

/// <summary>
/// 全局异常处理：统一输出 RFC 7807 ProblemDetails，5xx 不泄露内部细节。
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "设备未授权"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "资源不存在"),
            ArgumentException => (StatusCodes.Status400BadRequest, "请求参数不合法"),
            _ => (StatusCodes.Status500InternalServerError, "服务器内部错误")
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "未处理异常 {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        else
            logger.LogWarning(exception, "业务异常 {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = statusCode >= StatusCodes.Status500InternalServerError ? null : exception.Message,
            Instance = httpContext.Request.Path
        }, cancellationToken);

        return true;
    }
}