using System.Net.Mime;

namespace IdentityManagementSystem.UI.Infrastructure;

public sealed class GlobalExceptionHandlingMiddleware
{
    private const string GenericErrorMessage = "خطای غیرمنتظره‌ای رخ داد. لطفاً بعداً دوباره تلاش کنید.";

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
        var traceId = context.TraceIdentifier;

        _logger.LogError(exception,
            "Unhandled exception. TraceId: {TraceId}, Path: {Path}",
            traceId,
            context.Request.Path);

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        if (ExpectsJson(context.Request))
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                message = GenericErrorMessage,
                traceId
            });
            return;
        }

        context.Response.ContentType = MediaTypeNames.Text.Html + "; charset=utf-8";
        await context.Response.WriteAsync($$"""
            <!doctype html>
            <html lang="fa" dir="rtl">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>خطای سامانه</title>
                <style>
                    body { margin: 0; font-family: Tahoma, Arial, sans-serif; background: #f7f7f7; color: #222; }
                    main { max-width: 680px; margin: 12vh auto; padding: 32px; background: #fff; border: 1px solid #e5e5e5; border-radius: 8px; }
                    h1 { margin: 0 0 16px; font-size: 24px; }
                    p { line-height: 1.9; }
                    code { direction: ltr; unicode-bidi: plaintext; background: #f1f1f1; padding: 2px 6px; border-radius: 4px; }
                </style>
            </head>
            <body>
                <main>
                    <h1>خطای سامانه</h1>
                    <p>{{GenericErrorMessage}}</p>
                    <p>کد پیگیری: <code>{{traceId}}</code></p>
                </main>
            </body>
            </html>
            """);
    }

    private static bool ExpectsJson(HttpRequest request)
    {
        return request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
            || request.Headers.Accept.Any(value =>
                value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);
    }
}

public static class GlobalExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
    }
}
