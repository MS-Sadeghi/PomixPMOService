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
                // ایجاد درخواست
                var newRequest = new Request
                {
                    NationalId = model.NationalId,
                    MobileNumber = model.MobileNumber,
                    DocumentNumber = model.DocumentNumber,
                    VerificationCode = model.VerificationCode,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = User.Identity?.Name ?? "Unknown",
                    ValidateByExpert = null
                };

                _context.Request.Add(newRequest);
                await _context.SaveChangesAsync(); // RequestId تولید می‌شود

                // لاگ شاهکار
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

                // لاگ سند
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

                // لاگ اولیه وضعیت در RequestHistory
                var initialHistory = new RequestHistory
                {
                    RequestId = newRequest.RequestId,
                    StatusId = 1, // Pending
                    ExpertId = null, // هنوز کارشناس تعیین نشده
                    ActionDescription = "درخواست جدید ایجاد شد و در انتظار بررسی قرار گرفت.",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedStatus = "در انتظار بررسی",
                    UpdatedStatusBy = "سیستم",
                    UpdatedStatusDate = DateTime.UtcNow
                };
                _context.RequestHistory.Add(initialHistory);

                // ذخیره همه با هم
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
                _logger.LogError(ex, "Error in CreateNewRequest - UserId: {UserId}, Model: {@Model}", userId, model);
                return StatusCode(500, new { success = false, message = "خطا در ایجاد درخواست." });
            }
        }


        [HttpPost("ValidateRequest")]
        [Authorize(Policy = "CanValidateRequest")]
        public async Task<IActionResult> ValidateRequest([FromBody] ValidateRequestViewModel model)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out long userId))
                return Unauthorized("کاربر شناسایی نشد.");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var request = await _context.Request.FindAsync(model.RequestId);
                if (request == null)
                    return NotFound("درخواست یافت نشد.");

                // چک وضعیت فعلی (اختیاری: جلوگیری از تغییر مجدد)
                var currentStatus = await _context.RequestHistory
                    .Where(h => h.RequestId == model.RequestId)
                    .OrderByDescending(h => h.CreatedAt)
                    .FirstOrDefaultAsync();

                if (currentStatus != null &&
                    ((model.ValidateByExpert && currentStatus.StatusId == 2) ||
                     (!model.ValidateByExpert && currentStatus.StatusId == 3)))
                {
                    return BadRequest("این درخواست قبلاً بررسی شده است.");
                }

                // تعیین StatusId
                int newStatusId = model.ValidateByExpert ? 2 : 3; // 2=Approved, 3=Rejected
                string statusName = model.ValidateByExpert ? "تأیید شده" : "رد شده";

                // به‌روزرسانی Request
                request.ValidateByExpert = model.ValidateByExpert;
                request.Description = model.Description;
                request.UpdatedAt = DateTime.UtcNow;
                request.UpdatedBy = User.Identity?.Name ?? userId.ToString();

                _context.Request.Update(request);

                // ثبت در تاریخچه
                var history = new RequestHistory
                {
                    RequestId = request.RequestId,
                    ExpertId = userId,
                    StatusId = newStatusId,
                    ActionDescription = model.ValidateByExpert
                        ? $"درخواست تأیید شد. توضیحات: {model.Description}"
                        : $"درخواست رد شد. توضیحات: {model.Description}",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedStatus = statusName,
                    UpdatedStatusBy = User.Identity?.Name ?? userId.ToString(),
                    UpdatedStatusDate = DateTime.UtcNow
                };

                _context.RequestHistory.Add(history);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    RequestId = request.RequestId,
                    Message = model.ValidateByExpert
                        ? "درخواست با موفقیت تأیید شد."
                        : "درخواست با موفقیت رد شد."
                });
            }
            catch (Exception ex)
             {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error in ValidateRequest - RequestId: {RequestId}, UserId: {UserId}", model.RequestId, userId);
                return StatusCode(500, new { success = false, message = "خطا در سرور." });
            }
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
                    return Unauthorized(new { success = false, message = "کاربر شناسایی نشد." });

                var request = await _context.Request.FindAsync(requestId);
                if (request == null)
                    return NotFound(new { success = false, message = "درخواست یافت نشد." });

                // 🔥 اصلاح این بخش - فقط برای تأیید چک کنیم
                if (validateByExpert) // فقط وقتی می‌خواهد تأیید کند
                {
                    var latestLog = await _context.VerifyDocLog
                        .Where(v => v.RequestId == requestId)
                        .OrderByDescending(v => v.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (latestLog == null)
                        return BadRequest(new { success = false, message = "سند وجود ندارد. نمی‌توانید تأیید کنید." });

                    if (!(latestLog.IsRead ?? false))
                        return BadRequest(new { success = false, message = "ابتدا سند را بررسی کنید." });
                }

                int statusId = validateByExpert ? 2 : 3;
                string statusName = validateByExpert ? "تأیید شده" : "رد شده";

                // به‌روزرسانی Request
                request.ValidateByExpert = validateByExpert;
                request.UpdatedAt = DateTime.UtcNow;
                request.UpdatedBy = User.Identity?.Name ?? userId.ToString();
                _context.Request.Update(request);

                // ثبت در RequestHistory
                var history = new RequestHistory
                {
                    RequestId = requestId,
                    StatusId = statusId,
                    ExpertId = userId,
                    ActionDescription = $"{statusName} توسط کارشناس",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedStatus = statusName,
                    UpdatedStatusBy = User.Identity?.Name ?? userId.ToString(),
                    UpdatedStatusDate = DateTime.UtcNow
                };
                _context.RequestHistory.Add(history);

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


        // متد کمکی برای گرفتن آخرین وضعیت
        private async Task<RequestHistory?> GetCurrentStatusAsync(long requestId)
        {
            return await _context.RequestHistory
                .Include(h => h.Status) // برای دسترسی به StatusName
                .Where(h => h.RequestId == requestId)
                .OrderByDescending(h => h.CreatedAt) // ← ActionTime → CreatedAt
                .FirstOrDefaultAsync();
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

