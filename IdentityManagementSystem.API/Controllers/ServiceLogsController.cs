using IdentityManagementSystem.API.Data;
using IdentityManagementSystem.API.Models;
using IdentityManagementSystem.API.Modules.AccessControlReports.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IdentityManagementSystem.API.Controllers
{
    /// <summary>
    /// مشاهده‌ی لاگ فراخوانی سرویس‌های Pomix (فقط ادمین).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "AdminOnly")]
    public class ServiceLogsController : ControllerBase
    {
        private const int MaxPageSize = 500;
        private const int DefaultPageSize = 50;

        private readonly IdentityManagementSystemContext _context;
        private readonly PomixOptions _pomixOptions;

        public ServiceLogsController(IdentityManagementSystemContext context, IOptions<PomixOptions> pomixOptions)
        {
            _context = context;
            _pomixOptions = pomixOptions.Value;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            string? service,
            string? stage,
            DateTime? from,
            DateTime? to,
            int page = 1,
            int pageSize = DefaultPageSize)
        {
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
            page = Math.Max(page, 1);

            var query = _context.ServiceCallLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(service))
                query = query.Where(l => l.ServiceName == service);

            if (!string.IsNullOrWhiteSpace(stage))
                query = query.Where(l => l.Stage == stage);

            if (from.HasValue)
                query = query.Where(l => l.StartedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(l => l.StartedAt < to.Value);

            var totalCount = await query.CountAsync();
            var totalPages = Math.Max((int)Math.Ceiling(totalCount / (double)pageSize), 1);
            page = Math.Min(page, totalPages);

            var items = await query
                .OrderByDescending(l => l.StartedAt)
                .ThenByDescending(l => l.LogId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // خلاصه‌ی ۲۴ ساعت اخیر به تفکیک سرویس (مستقل از فیلترها)
            var since = DateTime.Now.AddHours(-24);
            var stuckBefore = DateTime.Now.AddMinutes(-2);

            var recent = await _context.ServiceCallLogs
                .AsNoTracking()
                .Where(l => l.StartedAt >= since)
                .Select(l => new { l.ServiceName, l.Stage, l.DurationMs, l.StartedAt })
                .ToListAsync();

            var summary = recent
                .GroupBy(l => l.ServiceName)
                .Select(g => new
                {
                    serviceName = g.Key,
                    total = g.Count(),
                    completed = g.Count(l => l.Stage == ServiceCallLog.StageCompleted),
                    failed = g.Count(l => l.Stage == ServiceCallLog.StageFailed),
                    timedOut = g.Count(l => l.Stage == ServiceCallLog.StageTimedOut),
                    // ارسال شده ولی بیش از ۲ دقیقه بدون پاسخ مانده است (یا job نگهداری آن را Abandoned کرده)
                    stuck = g.Count(l => l.Stage == ServiceCallLog.StageAbandoned
                                         || (l.Stage == ServiceCallLog.StageSent && l.StartedAt < stuckBefore)),
                    avgDurationMs = g.Where(l => l.DurationMs.HasValue).Select(l => l.DurationMs!.Value).DefaultIfEmpty().Average(),
                    lastCallAt = g.Max(l => l.StartedAt)
                })
                .OrderBy(s => s.serviceName)
                .ToList();

            // مصرف امروز نسبت به سقف روزانه‌ی Pomix (مستقل از فیلترها)
            var todayCount = await _context.ServiceCallLogs
                .CountAsync(l => l.StartedAt >= DateTime.Today);

            return Ok(new
            {
                summary,
                items,
                page,
                pageSize,
                totalCount,
                totalPages,
                todayCount,
                dailyLimit = _pomixOptions.DailyCallLimit
            });
        }
    }
}
