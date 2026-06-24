using System.Net;
using System.Text.Json;
using Bi.Api.Models;

namespace Bi.Api.Middlewares;

/// <summary>
/// 全局异常处理中间件
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发生未处理的异常: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json;charset=utf-8";
        
        var response = exception switch
        {
            UnauthorizedAccessException => new ApiResponse { Code = 401, Message = "未授权访问" },
            ArgumentException argEx => new ApiResponse { Code = 400, Message = argEx.Message },
            KeyNotFoundException => new ApiResponse { Code = 404, Message = "资源未找到" },
            _ => new ApiResponse { Code = 500, Message = "服务器内部错误" }
        };

        context.Response.StatusCode = response.Code switch
        {
            401 => (int)HttpStatusCode.Unauthorized,
            400 => (int)HttpStatusCode.BadRequest,
            404 => (int)HttpStatusCode.NotFound,
            _ => (int)HttpStatusCode.InternalServerError
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}

public static class ExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
        => app.UseMiddleware<ExceptionMiddleware>();
}

