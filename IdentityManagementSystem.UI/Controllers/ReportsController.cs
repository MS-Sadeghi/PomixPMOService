using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IdentityManagementSystem.API.Data;
using IdentityManagementSystem.API.Models;

namespace IdentityManagementSystem.UI.Controllers
{
    public class ReportsController : Controller
    {
        private readonly IdentityManagementSystemContext _context;

        public ReportsController(IdentityManagementSystemContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string docNumber = "")
        {
            var model = new ReportsViewModel();

            // 1. جستجوی سند خاص
            if (!string.IsNullOrEmpty(docNumber))
            {
                model.SearchedDocNumber = docNumber;
                model.DocRequestCount = await _context.Request
                    .CountAsync(r => r.DocumentNumber == docNumber);
            }

            // 2. 10 سند پر درخواست
            model.TopDocuments = await _context.Request
                .GroupBy(r => r.DocumentNumber)
                .Select(g => new DocStats
                {
                    DocumentNumber = g.Key ?? "نامشخص",
                    RequestCount = g.Count()
                })
                .OrderByDescending(x => x.RequestCount)
                .Take(10)
                .ToListAsync();

            // 3. کاربر برتر (هفته، ماه، سال)
            var now = DateTime.UtcNow;
            model.TopUserWeek = await GetTopUser(now.AddDays(-7), now);
            model.TopUserMonth = await GetTopUser(now.AddMonths(-1), now);
            model.TopUserYear = await GetTopUser(now.AddYears(-1), now);

            // 4. آمار کلی
            var total = await _context.Request.CountAsync();
            var approved = await _context.Request.CountAsync(r => r.ValidateByExpert == true);
            var rejected = await _context.Request.CountAsync(r => r.ValidateByExpert == false);

            model.Stats = new RequestStats
            {
                Total = total,
                Approved = approved,
                Rejected = rejected,
                Pending = total - approved - rejected
            };

            return View(model);
        }

        private async Task<UserStats> GetTopUser(DateTime from, DateTime to)
        {
            var top = await _context.Request
                .Where(r => r.CreatedAt >= from && r.CreatedAt <= to)
                .GroupBy(r => r.CreatedBy)
                .Select(g => new { User = g.Key ?? "ناشناس", Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .FirstOrDefaultAsync();

            return top != null
                ? new UserStats { Username = top.User, RequestCount = top.Count }
                : new UserStats { Username = "هیچ", RequestCount = 0 };
        }
    }

    // مدل‌های گزارش
    public class ReportsViewModel
    {
        public string? SearchedDocNumber { get; set; }
        public int DocRequestCount { get; set; }
        public List<DocStats> TopDocuments { get; set; } = new();
        public UserStats TopUserWeek { get; set; } = new();
        public UserStats TopUserMonth { get; set; } = new();
        public UserStats TopUserYear { get; set; } = new();
        public RequestStats Stats { get; set; } = new();
    }

    public class DocStats
    {
        public string DocumentNumber { get; set; } = "";
        public int RequestCount { get; set; }
    }

    public class UserStats
    {
        public string Username { get; set; } = "";
        public int RequestCount { get; set; }
    }

    public class RequestStats
    {
        public int Total { get; set; }
        public int Approved { get; set; }
        public int Rejected { get; set; }
        public int Pending { get; set; }
    }
}