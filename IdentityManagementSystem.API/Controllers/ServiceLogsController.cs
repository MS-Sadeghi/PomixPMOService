using IdentityManagementSystem.API.Data;
using IdentityManagementSystem.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        private const int MaxRows = 500;

        private readonly IdentityManagementSystemContext _context;

        public ServiceLogsController(IdentityManagementSystemContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            string? service,
            string? stage,
            DateTime? from,
            DateTime? to,
            int top = 100)
        {
            top = Math.Clamp(top, 1, MaxRows);

            var query = _context.ServiceCallLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(service))
                query = query.Where(l => l.ServiceName == service);

            if (!string.IsNullOrWhiteSpace(stage))
                query = query.Where(l => l.Stage == stage);

            if (from.HasValue)
                query = query.Where(l => l.StartedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(l => l.StartedAt < to.Value);

            var items = await query
                .OrderByDescending(l => l.StartedAt)
                .Take(top)
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
                    // ارسال شده ولی بیش از ۲ دقیقه بدون پاسخ مانده است
                    stuck = g.Count(l => l.Stage == ServiceCallLog.StageSent && l.StartedAt < stuckBefore),
                    avgDurationMs = g.Where(l => l.DurationMs.HasValue).Select(l => l.DurationMs!.Value).DefaultIfEmpty().Average(),
                    lastCallAt = g.Max(l => l.StartedAt)
                })
                .OrderBy(s => s.serviceName)
                .ToList();

            return Ok(new { summary, items });
        }
    }
}
