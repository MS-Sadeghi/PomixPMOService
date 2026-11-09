using IdentityManagementSystem.API.Data;
using IdentityManagementSystem.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.Common
{
    /// <summary>
    /// رکوردهای Sent که مدت‌ها بدون پاسخ مانده‌اند (مثلاً برنامه وسط فراخوانی ری‌استارت شده)
    /// را به Abandoned تبدیل می‌کند تا برای همیشه «در انتظار پاسخ» نمانند.
    /// خطا در این job روی هیچ فراخوانی‌ای اثر ندارد.
    /// </summary>
    public class ServiceCallLogMaintenanceService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ServiceCallLogMaintenanceService> _logger;
        private readonly PomixOptions _options;

        public ServiceCallLogMaintenanceService(
            IServiceScopeFactory scopeFactory,
            ILogger<ServiceCallLogMaintenanceService> logger,
            IOptions<PomixOptions> options)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);

            do
            {
                await AbandonStaleAsync(stoppingToken);
            }
            while (await WaitNextAsync(timer, stoppingToken));
        }

        private static async Task<bool> WaitNextAsync(PeriodicTimer timer, CancellationToken token)
        {
            try
            {
                return await timer.WaitForNextTickAsync(token);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private async Task AbandonStaleAsync(CancellationToken token)
        {
            try
            {
                var minutes = _options.StaleSentMinutes > 0 ? _options.StaleSentMinutes : 5;
                var cutoff = DateTime.Now.AddMinutes(-minutes);

                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IdentityManagementSystemContext>();

                var affected = await context.ServiceCallLogs
                    .Where(l => l.Stage == ServiceCallLog.StageSent && l.StartedAt < cutoff)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(l => l.Stage, ServiceCallLog.StageAbandoned)
                        .SetProperty(l => l.ErrorMessage, "No response was recorded for this call (it was likely interrupted)."),
                        token);

                if (affected > 0)
                    _logger.LogInformation("Marked {Count} stale service call log(s) as Abandoned.", affected);
            }
            catch (OperationCanceledException)
            {
                // خاموش شدن برنامه
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Service call log maintenance failed.");
            }
        }
    }
}
