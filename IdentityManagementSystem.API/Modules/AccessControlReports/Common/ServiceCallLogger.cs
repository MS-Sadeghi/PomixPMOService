using System.Security.Claims;
using System.Text.Json;
using IdentityManagementSystem.API.Data;
using IdentityManagementSystem.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.Common
{
    /// <summary>
    /// ثبت مراحل فراخوانی سرویس‌های Pomix در Log.ServiceCallLog.
    /// خطا در ثبت لاگ هیچ‌وقت نباید روی فراخوانی اصلی اثر بگذارد.
    /// </summary>
    public class ServiceCallLogger
    {
        private const int MaxParamsLength = 2000;
        private const int MaxErrorLength = 1000;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ServiceCallLogger> _logger;
        private readonly PomixOptions _options;

        public ServiceCallLogger(
            IServiceScopeFactory scopeFactory,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ServiceCallLogger> logger,
            IOptions<PomixOptions> options)
        {
            _scopeFactory = scopeFactory;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _options = options.Value;
        }

        /// <summary>قبل از ارسال درخواست: رکورد با Stage = Sent ساخته می‌شود.</summary>
        public async Task<long?> StartAsync(string serviceName, string? endpoint, object[] parameters)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                var userIdClaim = httpContext?.User?.FindFirst("UserId")?.Value;

                var entry = new ServiceCallLog
                {
                    ServiceName = Truncate(serviceName, 100)!,
                    Endpoint = Truncate(endpoint, 300),
                    RequestParams = Truncate(SafeParams(parameters), MaxParamsLength),
                    UserId = long.TryParse(userIdClaim, out var userId) ? userId : null,
                    Username = Truncate(httpContext?.User?.FindFirst("Username")?.Value, 100),
                    IpAddress = Truncate(httpContext?.Connection?.RemoteIpAddress?.ToString(), 45),
                    TraceId = Truncate(httpContext?.TraceIdentifier, 64),
                    StartedAt = DateTime.Now,
                    Stage = ServiceCallLog.StageSent
                };

                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IdentityManagementSystemContext>();
                context.ServiceCallLogs.Add(entry);
                await context.SaveChangesAsync();

                await WarnIfNearQuotaAsync(context, entry.StartedAt);

                return entry.LogId;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to write service call log (start) for {Service}", serviceName);
                return null;
            }
        }

        // فقط هشدار می‌دهد؛ هیچ‌وقت فراخوانی را رد نمی‌کند و خطایش به بیرون نمی‌رسد.
        private async Task WarnIfNearQuotaAsync(IdentityManagementSystemContext context, DateTime now)
        {
            if (_options.DailyCallLimit <= 0)
                return;

            try
            {
                var todayCount = await context.ServiceCallLogs
                    .CountAsync(l => l.StartedAt >= now.Date);

                var warnAt = _options.DailyCallLimit * Math.Clamp(_options.QuotaWarnPercent, 1, 100) / 100;

                if (todayCount >= _options.DailyCallLimit)
                    _logger.LogError("Pomix daily call limit reached: {Count}/{Limit} calls today.", todayCount, _options.DailyCallLimit);
                else if (todayCount >= warnAt)
                    _logger.LogWarning("Pomix daily call usage is high: {Count}/{Limit} calls today.", todayCount, _options.DailyCallLimit);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to check Pomix daily call quota.");
            }
        }

        /// <summary>بعد از پایان فراخوانی (موفق، خطا یا تایم‌اوت) رکورد به‌روزرسانی می‌شود.</summary>
        public async Task FinishAsync(
            long? logId,
            string stage,
            int durationMs,
            int? httpStatusCode = null,
            int? pomixStatusCode = null,
            int? responseSize = null,
            string? errorMessage = null)
        {
            if (logId == null)
                return;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IdentityManagementSystemContext>();
                var entry = await context.ServiceCallLogs.FindAsync(logId.Value);
                if (entry == null)
                    return;

                entry.Stage = stage;
                entry.CompletedAt = DateTime.Now;
                entry.DurationMs = durationMs;
                entry.HttpStatusCode = httpStatusCode;
                entry.PomixStatusCode = pomixStatusCode;
                entry.ResponseSize = responseSize;
                entry.ErrorMessage = Truncate(errorMessage, MaxErrorLength);

                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to write service call log (finish) for log {LogId}", logId);
            }
        }

        // پارامترها را بدون اطلاعات ورود (credentials و password) ذخیره می‌کند
        private static string? SafeParams(object[] parameters)
        {
            try
            {
                var safe = parameters.Select(p =>
                {
                    var element = JsonSerializer.SerializeToElement(p);
                    var name = element.ValueKind == JsonValueKind.Object &&
                               element.TryGetProperty("parameterName", out var n)
                        ? n.GetString()
                        : null;

                    if (string.Equals(name, "credentials", StringComparison.OrdinalIgnoreCase) ||
                        (name?.Contains("password", StringComparison.OrdinalIgnoreCase) ?? false))
                    {
                        return new { parameterName = name, parameterValue = (object)"***" };
                    }

                    element.TryGetProperty("parameterValue", out var value);
                    return new { parameterName = name, parameterValue = (object)value.Clone() };
                });

                return JsonSerializer.Serialize(safe, new JsonSerializerOptions
                {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
            }
            catch
            {
                return null;
            }
        }

        private static string? Truncate(string? value, int max)
            => value == null ? null : (value.Length <= max ? value : value[..max]);
    }
}
