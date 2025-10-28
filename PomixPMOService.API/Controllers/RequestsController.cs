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
        private readonly IdentityManagementSystemContext _context;
        private readonly ILogger<RequestController> _logger;
        private readonly HttpClient _client;

        public RequestController(IdentityManagementSystemContext context, ILogger<RequestController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<PaginatedResponse<RequestViewModel>>> GetAll(int page = 1, int pageSize = 10, string search = "")
        {
            var query = _context.Request.AsQueryable();

            // اعمال فیلتر جستجو
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(r => r.NationalId.Contains(search) ||
                                        r.MobileNumber.Contains(search) ||
                                        r.DocumentNumber.Contains(search));
            }

            query = query.OrderByDescending(r => r.CreatedAt);

            // شمارش کل ردیف‌ها
            var totalCount = await query.CountAsync();

            // اعمال صفحه‌بندی
            var items = await query
                .Select(r => new RequestViewModel
                {
                    RequestId = r.RequestId,
                    RequestCode = r.RequestCode,
                    NationalId = r.NationalId,
                    MobileNumber = r.MobileNumber,
                    DocumentNumber = r.DocumentNumber,
                    VerificationCode = r.VerificationCode,
                    IsMatch = r.IsMatch ?? false,
                    IsExist = r.IsExist ?? false,
                    IsNationalIdInResponse = r.IsNationalIdInResponse ?? false,
                    IsNationalIdInLawyers = r.IsNationalIdInLawyers ?? false,
                    ValidateByExpert = r.ValidateByExpert,
                    Description = r.Description,
                    CreatedAt = r.CreatedAt,
                    CreatedBy = r.CreatedBy
                })
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var response = new PaginatedResponse<RequestViewModel>
            {
                Items = items,
                TotalCount = totalCount,
                CurrentPage = page,
                PageSize = pageSize
            };

            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RequestViewModel>> Get(long id)
        {
            var request = await _context.Request
                .Where(r => r.RequestId == id)
                .Select(r => new RequestViewModel
                {
                    RequestId = r.RequestId,
                    RequestCode = r.RequestCode,
                    NationalId = r.NationalId,
                    MobileNumber = r.MobileNumber,
                    DocumentNumber = r.DocumentNumber,
                    VerificationCode = r.VerificationCode,
                    IsMatch = r.IsMatch ?? false,
                    IsExist = r.IsExist ?? false,
                    IsNationalIdInResponse = r.IsNationalIdInResponse ?? false,
                    IsNationalIdInLawyers = r.IsNationalIdInLawyers ?? false, // اضافه شده
                    ValidateByExpert = r.ValidateByExpert, // اضافه شده
                    Description = r.Description,           // اضافه شده
                    CreatedAt = r.CreatedAt,
                    CreatedBy = r.CreatedBy
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
                await _context.SaveChangesAsync();

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

                var verifyLog = new VerifyDocLog
                {
                    DocumentNumber = model.DocumentNumber,
                    VerificationCode = model.VerificationCode,
                    ResponseText = "",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId.ToString(),
                    RequestId = newRequest.RequestId
                };
                _context.VerifyDocLog.Add(verifyLog);
                await _context.SaveChangesAsync();

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

        [HttpPost("ValidateRequest")]
        [Authorize(Policy = "CanValidateRequest")]
        public async Task<IActionResult> ValidateRequest([FromBody] ValidateRequestViewModel model)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out long userId))
            {
                return Unauthorized("کاربر شناسایی نشد.");
            }

            var request = await _context.Request.FindAsync(model.RequestId);
            if (request == null)
                return NotFound("درخواست یافت نشد.");

            request.ValidateByExpert = model.ValidateByExpert;
            request.Description = model.Description;
            request.UpdatedAt = DateTime.UtcNow;
            request.UpdatedBy = User.Identity?.Name ?? userId.ToString();


            _context.Request.Update(request);
            await _context.SaveChangesAsync();

            // لاگ کردن عملیات
            _context.RequestLogs.Add(new RequestLog
            {
                RequestId = request.RequestId,
                UserId = userId,
                Action = model.ValidateByExpert ? "ValidateRequest_Approved" : "ValidateRequest_Rejected",
                Details = model.ValidateByExpert
             ? $"درخواست در تاریخ {DateTime.UtcNow:yyyy/MM/dd HH:mm:ss} توسط کارشناس تأیید شد. توضیحات: {model.Description}"
             : $"درخواست در تاریخ {DateTime.UtcNow:yyyy/MM/dd HH:mm:ss} توسط کارشناس رد شد. توضیحات: {model.Description}",
                ActionTime = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return Ok(new
            {
                RequestId = request.RequestId,
                Message = model.ValidateByExpert ? "درخواست با موفقیت تأیید شد." : "درخواست رد شد."
            });
        }


        [HttpPost("UpdateValidationStatus")]
        [Authorize(Policy = "CanValidateRequest")]
        public async Task<IActionResult> UpdateValidationStatus([FromBody] UpdateValidationStatusViewModel model)
        {
            var requestId = model.RequestId;
            var validateByExpert = model.ValidateByExpert;

            try
            {
                var userIdClaim = User.FindFirst("UserId")?.Value;
                if (!long.TryParse(userIdClaim, out long userId))
                    return Unauthorized("کاربر شناسایی نشد.");

                var request = await _context.Request.FindAsync(requestId);
                if (request == null)
                    return NotFound("درخواست یافت نشد.");

                // چک کردن آخرین لاگ
                var latestLog = await _context.VerifyDocLog
                    .Where(v => v.RequestId == requestId)
                    .OrderByDescending(v => v.CreatedAt)
                    .FirstOrDefaultAsync();

                // اگر سند وجود ندارد → فقط اجازه رد
                if (latestLog == null)
                {
                    if (validateByExpert)
                    {
                        return new JsonResult(new
                        {
                            success = false,
                            message = "سند وجود ندارد. نمی‌توانید درخواست را تأیید کنید."
                        })
                        { StatusCode = 400 };
                    }
                    // رد مجاز است
                }
                // اگر سند وجود دارد → باید خوانده شده باشد
                else if (!(latestLog.IsRead ?? false))
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = "برای تأیید یا رد درخواست، ابتدا باید متن سند را بررسی و گزینه «متن سند بررسی شد» را تیک بزنید."
                    })
                    { StatusCode = 400 };
                }

                // به‌روزرسانی وضعیت
                request.ValidateByExpert = validateByExpert;
                request.UpdatedAt = DateTime.UtcNow;
                request.UpdatedBy = User.Identity?.Name ?? userId.ToString();

                _context.Request.Update(request);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = validateByExpert ? "درخواست با موفقیت تأیید شد." : "درخواست با موفقیت رد شد."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateValidationStatus for RequestId: {RequestId}", requestId);
                return StatusCode(500, new { success = false, message = "خطا در سرور." });
            }
        }


    }

    public class NewRequestViewModel
    {
        public string NationalId { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public string VerificationCode { get; set; } = string.Empty;
    }
    public class UpdateValidationStatusViewModel
    {
        public long RequestId { get; set; }
        public bool ValidateByExpert { get; set; }
    }


}

