using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PomixPMOService.API.Models.ViewModels;
using ServicePomixPMO.API.Data;
using ServicePomixPMO.API.Models;

namespace ServicePomixPMO.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RequestsController : ControllerBase
    {
        private readonly PomixServiceContext _context;

        public RequestsController(PomixServiceContext context)
        {
            _context = context;
        }

        // GET: api/requests
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RequestViewModel>>> GetRequests()
        {
            return await _context.Request
            .Include(r => r.Expert) // navigation property
            .Select(r => new RequestViewModel
            {
                RequestId = r.RequestId,
                NationalId = r.NationalId,
                DocumentNumber = r.DocumentNumber,
                VerificationCode = r.VerificationCode,
                IdentityVerified = r.IdentityVerified,
                DocumentVerified = r.DocumentVerified,
                DocumentMatch = r.DocumentMatch,
                TextApproved = r.TextApproved,
                ExpertId = r.ExpertId,
                ExpertName = r.Expert != null ? $"{r.Expert.Name} {r.Expert.LastName}" : null,
                RequestStatus = r.RequestStatus,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                DocumentText = r.DocumentText,
                MobileNumber = r.MobileNumber
            })
            .ToListAsync();


        }

        // GET: api/requests/5
        [HttpGet("{id}")]
        public async Task<ActionResult<RequestViewModel>> GetRequest(long id)
        {
            var request = await _context.Request
                .Include(r => r.Expert)
                .Select(r => new RequestViewModel
                {
                    RequestId = r.RequestId,
                    NationalId = r.NationalId,
                    DocumentNumber = r.DocumentNumber,
                    VerificationCode = r.VerificationCode,
                    IdentityVerified = r.IdentityVerified,
                    DocumentVerified = r.DocumentVerified,
                    DocumentMatch = r.DocumentMatch,
                    TextApproved = r.TextApproved,
                    ExpertId = r.ExpertId,
                    ExpertName = r.Expert != null ? $"{r.Expert.Name} {r.Expert.LastName}" : null,
                    RequestStatus = r.RequestStatus,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt,
                    DocumentText = r.DocumentText
                })
                .FirstOrDefaultAsync(r => r.RequestId == id);

            if (request == null)
                return NotFound();

            return request;
        }

        // POST: api/requests
        [HttpPost]
        public async Task<ActionResult<RequestViewModel>> CreateRequest(CreateRequestViewModel viewModel)
        {
            var request = new Request
            {
                NationalId = viewModel.NationalId,
                DocumentNumber = viewModel.DocumentNumber,
                VerificationCode = viewModel.VerificationCode,
                DocumentText = viewModel.DocumentText
            };

            _context.Request.Add(request);
            await _context.SaveChangesAsync();

            // ثبت لاگ
            _context.RequestLogs.Add(new RequestLog
            {
                RequestId = request.RequestId,
                Action = "Create",
                Details = $"درخواست جدید با شماره سند {request.DocumentNumber} ایجاد شد"
            });
            await _context.SaveChangesAsync();

            var result = new RequestViewModel
            {
                RequestId = request.RequestId,
                NationalId = request.NationalId,
                DocumentNumber = request.DocumentNumber,
                VerificationCode = request.VerificationCode,
                IdentityVerified = request.IdentityVerified,
                DocumentVerified = request.DocumentVerified,
                DocumentMatch = request.DocumentMatch,
                TextApproved = request.TextApproved,
                RequestStatus = request.RequestStatus,
                CreatedAt = request.CreatedAt,
                UpdatedAt = request.UpdatedAt,
                DocumentText = request.DocumentText

            };

            return CreatedAtAction(nameof(GetRequest), new { id = request.RequestId }, result);
        }

        // PUT: api/requests/5/approve-text
        [HttpPut("{id}/approve-text")]
        public async Task<IActionResult> ApproveText(long id, ApproveTextViewModel viewModel)
        {
            var request = await _context.Request.FindAsync(id);
            if (request == null)
                return NotFound();

            request.TextApproved = viewModel.IsApproved;
            request.UpdatedAt = DateTime.UtcNow;
            request.RequestStatus = viewModel.IsApproved ? "Approved" : "Rejected";
            // فرض می‌کنیم ExpertId از توکن احراز هویت دریافت می‌شود
            request.ExpertId = 1; // جایگزین با ID کاربر فعلی

            await _context.SaveChangesAsync();

            // ثبت لاگ
            _context.RequestLogs.Add(new RequestLog
            {
                RequestId = id,
                UserId = request.ExpertId,
                Action = "TextApproval",
                Details = $"متن سند توسط کارشناس {(viewModel.IsApproved ? "تأیید" : "رد")} شد"
            });
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}