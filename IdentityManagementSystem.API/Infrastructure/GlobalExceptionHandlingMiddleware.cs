using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace IdentityManagementSystem.API.Infrastructure;

public sealed class GlobalExceptionHandlingMiddleware
{
    private const string GenericErrorMessage = "خطای غیرمنتظره‌ای رخ داد. لطفاً بعداً دوباره تلاش کنید.";
    private const string BadRequestMessage = "درخواست ارسال‌شده معتبر نیست.";
    private const string ForbiddenMessage = "شما مجوز انجام این عملیات را ندارید.";

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
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
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogError(exception, "Unhandled exception after response started. TraceId: {TraceId}", context.TraceIdentifier);
                throw;
            }

            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var statusCode = GetStatusCode(exception);
        var traceId = context.TraceIdentifier;

        _logger.LogError(exception,
            "Unhandled exception. StatusCode: {StatusCode}, TraceId: {TraceId}, Path: {Path}",
            statusCode,
            traceId,
            context.Request.Path);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = GetTitle(statusCode),
            Detail = GetSafeDetail(statusCode),
            Instance = context.Request.Path
        };
        problemDetails.Extensions["traceId"] = traceId;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json; charset=utf-8";

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
    }

    private static int GetStatusCode(Exception exception)
    {
        return exception switch
        {
            ArgumentException => StatusCodes.Status400BadRequest,
            BadHttpRequestException => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };
    }

    private static string GetTitle(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "Bad Request",
            StatusCodes.Status403Forbidden => "Forbidden",
            _ => "Internal Server Error"
        };
    }

    private static string GetSafeDetail(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => BadRequestMessage,
            StatusCodes.Status403Forbidden => ForbiddenMessage,
            _ => GenericErrorMessage
        };
    }
}

public static class GlobalExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
    }
}
