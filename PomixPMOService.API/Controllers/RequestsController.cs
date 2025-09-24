using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PomixPMOService.API.Models.ViewModels;
using ServicePomixPMO.API.Data;
using ServicePomixPMO.API.Models;

namespace ServicePomixPMO.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RequestsController : ControllerBase
    {
        private readonly PomixServiceContext _context;

        public RequestsController(PomixServiceContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RequestViewModel>>> GetRequests()
        {
            var callerUserId = long.Parse(User.FindFirst("UserId")?.Value ?? "0");
            if (callerUserId == 0)
                return Unauthorized("کاربر شناسایی نشد.");

            return await _context.Request
                .Include(r => r.Expert)
                .Where(r => r.ExpertId == callerUserId)
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

        [HttpGet("{id}")]
        public async Task<ActionResult<RequestViewModel>> GetRequest(long id)
        {
            var callerUserId = long.Parse(User.FindFirst("UserId")?.Value ?? "0");
            if (callerUserId == 0)
                return Unauthorized("کاربر شناسایی نشد.");

            var request = await _context.Request
                .Include(r => r.Expert)
                .Where(r => r.ExpertId == callerUserId)
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
                .FirstOrDefaultAsync(r => r.RequestId == id);

            if (request == null)
                return NotFound();

            return request;
        }

        [HttpPost]
        public async Task<ActionResult<RequestViewModel>> CreateRequest(CreateRequestViewModel viewModel)
        {
            var callerUserId = long.Parse(User.FindFirst("UserId")?.Value ?? "0");
            if (callerUserId == 0)
                return Unauthorized("کاربر شناسایی نشد.");

            var request = new Request
            {
                NationalId = viewModel.NationalId,
                DocumentNumber = viewModel.DocumentNumber,
                VerificationCode = viewModel.VerificationCode,
                DocumentText = viewModel.DocumentText,
                ExpertId = callerUserId,
                CreatedAt = DateTime.UtcNow,
                RequestStatus = "Pending",
                MobileNumber = viewModel.MobileNumber
            };

            _context.Request.Add(request);
            await _context.SaveChangesAsync();

            _context.RequestLogs.Add(new RequestLog
            {
                RequestId = request.RequestId,
                UserId = callerUserId,
                Action = "Create",
                Details = $"درخواست جدید با شماره سند {request.DocumentNumber} ایجاد شد",
                ActionTime = DateTime.UtcNow
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
                DocumentText = request.DocumentText,
                MobileNumber = request.MobileNumber
            };

            return CreatedAtAction(nameof(GetRequest), new { id = request.RequestId }, result);
        }

        [HttpPut("{id}/approve-text")]
        public async Task<IActionResult> ApproveText(long id, ApproveTextViewModel viewModel)
        {
            var callerUserId = long.Parse(User.FindFirst("UserId")?.Value ?? "0");
            if (callerUserId == 0)
                return Unauthorized("کاربر شناسایی نشد.");

            var request = await _context.Request.FindAsync(id);
            if (request == null)
                return NotFound();

            if (request.ExpertId != callerUserId)
                return Forbid("شما مجاز به ویرایش این درخواست نیستید.");

            request.TextApproved = viewModel.IsApproved;
            request.UpdatedAt = DateTime.UtcNow;
            request.RequestStatus = viewModel.IsApproved ? "Approved" : "Rejected";
            request.ExpertId = callerUserId;

            await _context.SaveChangesAsync();

            _context.RequestLogs.Add(new RequestLog
            {
                RequestId = id,
                UserId = callerUserId,
                Action = "TextApproval",
                Details = $"متن سند توسط کارشناس {(viewModel.IsApproved ? "تأیید" : "رد")} شد",
                ActionTime = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}