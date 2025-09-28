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
    public class RequestController : ControllerBase
    {
        private readonly PomixServiceContext _context;

        public RequestController(PomixServiceContext context)
        {
            _context = context;
        }

        // GET: api/NewRequest
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RequestViewModel>>> GetAll()
        {
            return await _context.Request
                .Select(r => new RequestViewModel
                {
                    RequestId = r.RequestId,
                    RequestCode = (string)r.RequestCode,
                    NationalId = r.NationalId,
                    MobileNumber = r.MobileNumber,
                    DocumentNumber = r.DocumentNumber,
                    VerificationCode = r.VerificationCode,
                    IsMatch = r.IsMatch ?? false,
                    IsExist = r.IsExist,
                    IsNationalIdInResponse = r.IsNationalIdInResponse,
                    CreatedAt = r.CreatedAt,
                    CreatedBy = r.CreatedBy,
               
                })
                .ToListAsync();
        }

        // GET: api/NewRequest/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<RequestViewModel>> Get(long id)
        {
            var request = await _context.Request
                .Where(r => r.RequestId == id)
                .Select(r => new RequestViewModel
                {
                    RequestId = r.RequestId,
                    RequestCode = (string)r.RequestCode,
                    NationalId = r.NationalId,
                    MobileNumber = r.MobileNumber,
                    DocumentNumber = r.DocumentNumber,
                    VerificationCode = r.VerificationCode,
                    IsMatch = (bool)r.IsMatch,
                    IsExist = r.IsExist,
                    IsNationalIdInResponse = r.IsNationalIdInResponse,
                    CreatedAt = r.CreatedAt,
                    CreatedBy = r.CreatedBy,
                   
                })
                .FirstOrDefaultAsync();

            if (request == null)
                return NotFound();

            return request;
        }
        [HttpPost("CreateNewRequest")]
        [Authorize]
        public async Task<IActionResult> CreateNewRequest(NewRequestViewModel model)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out long userId))
            {
                return Unauthorized("کاربر شناسایی نشد.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1️⃣ ایجاد رکورد Request
                var newRequest = new Request
                {
                    NationalId = model.NationalId,
                    MobileNumber = model.MobileNumber,
                    DocumentNumber = model.DocumentNumber,
                    VerificationCode = model.VerificationCode,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = User.Identity?.Name ?? "Unknown"
                };
                _context.Request.Add(newRequest);
                await _context.SaveChangesAsync(); // اینجا RequestId تولید میشه

                // 2️⃣ ایجاد ShahkarLog مرتبط
                var shahkarLog = new ShahkarLog
                {
                    NationalId = model.NationalId,
                    MobileNumber = model.MobileNumber,
                    RequestCode = newRequest.RequestCode,
                    ExpertId = userId,
                    RequestId = newRequest.RequestId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.ShahkarLog.Add(shahkarLog);
                await _context.SaveChangesAsync();

                // 3️⃣ ایجاد VerifyDocLog مرتبط
                var verifyLog = new VerifyDocLog
                {
                    DocumentNumber = model.DocumentNumber,
                    VerificationCode = model.VerificationCode,
                    ResponseText = "", // میتونی بعداً مقدار واقعی پاسخ سرویس رو بذاری
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId.ToString(),
                    RequestId = newRequest.RequestId
                };
                _context.VerifyDocLog.Add(verifyLog);
                await _context.SaveChangesAsync();

                // تایید transaction
                await transaction.CommitAsync();

                return Ok(new
                {
                    RequestId = newRequest.RequestId,
                    Message = "درخواست جدید با موفقیت ایجاد شد."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"خطا در ایجاد درخواست: {ex.Message}");
            }
        }

        public class NewRequestViewModel
        {
            public string NationalId { get; set; } = string.Empty;       // کد ملی کاربر
            public string MobileNumber { get; set; } = string.Empty;     // شماره موبایل
            public string DocumentNumber { get; set; } = string.Empty;   // شماره سند / DocumentNumber
            public string VerificationCode { get; set; } = string.Empty; // VerificationCode / SecretNo
        }


    }
}
