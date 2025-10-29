using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PomixPMOService.API.Models.ViewModels;
using ServicePomixPMO.API.Data;
using ServicePomixPMO.API.Models;
using ServicePomixPMO.API.Services.Logging;

namespace ServicePomixPMO.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RequestController : ControllerBase
    {
        private readonly IdentityManagementSystemContext _context;
        private readonly ILogger<RequestController> _logger;
        private readonly UserActionLogger _actionLogger;

        public RequestController(
            IdentityManagementSystemContext context,
            ILogger<RequestController> logger,
            UserActionLogger actionLogger)
        {
            _context = context;
            _logger = logger;
            _actionLogger = actionLogger;
        }

        [HttpGet]
        public async Task<ActionResult<PaginatedResponse<RequestViewModel>>> GetAll(int page = 1, int pageSize = 10, string search = "")
        {
            var query = _context.Request.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(r => r.NationalId.Contains(search) ||
                                        r.MobileNumber.Contains(search) ||
                                        r.DocumentNumber.Contains(search));
            }

            query = query.OrderByDescending(r => r.CreatedAt);

            var totalCount = await query.CountAsync();

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

            return Ok(new PaginatedResponse<RequestViewModel>
            {
                Items = items,
                TotalCount = totalCount,
                CurrentPage = page,
                PageSize = pageSize
            });
        }

        [HttpPost("CreateNewRequest")]
        [Authorize]
        public async Task<IActionResult> CreateNewRequest(NewRequestViewModel model)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out long userId))
                return Unauthorized("کاربر شناسایی نشد.");

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
                    CreatedBy = User.Identity?.Name ?? "Unknown",
                    ValidateByExpert = null
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

                var initialHistory = new RequestHistory
                {
                    RequestId = newRequest.RequestId,
                    StatusId = 1,
                    ExpertId = null,
                    ActionDescription = "درخواست جدید ایجاد شد و در انتظار بررسی قرار گرفت.",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedStatus = "در انتظار بررسی",
                    UpdatedStatusBy = "سیستم",
                    UpdatedStatusDate = DateTime.UtcNow
                };
                _context.RequestHistory.Add(initialHistory);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _actionLogger.Info(userId, "Create_Request", $"RequestId={newRequest.RequestId}, Document={model.DocumentNumber}");

                return Ok(new
                {
                    RequestId = newRequest.RequestId,
                    Message = "درخواست جدید با موفقیت ایجاد شد."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error in CreateNewRequest");
                await _actionLogger.Error(userId, "Create_Request", $"Exception: {ex.Message}");
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

                int newStatusId = model.ValidateByExpert ? 2 : 3;
                string statusName = model.ValidateByExpert ? "تأیید شده" : "رد شده";

                request.ValidateByExpert = model.ValidateByExpert;
                request.Description = model.Description;
                request.UpdatedAt = DateTime.UtcNow;
                request.UpdatedBy = User.Identity?.Name ?? userId.ToString();
                _context.Request.Update(request);

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

                await _actionLogger.Info(userId, "Validate_Request", $"{statusName} by expert");

                return Ok(new
                {
                    RequestId = request.RequestId,
                    Message = $"درخواست با موفقیت {statusName}."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error in ValidateRequest");
                await _actionLogger.Error(userId, "Validate_Request", $"Exception: {ex.Message}");
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

                if (validateByExpert)
                {
                    var latestLog = await _context.VerifyDocLog
                        .Where(v => v.RequestId == requestId)
                        .OrderByDescending(v => v.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (latestLog == null)
                        return BadRequest(new { success = false, message = "❌ سند مربوط به این درخواست وجود ندارد. ابتدا باید سند را بررسی کنید." });

                    if (!(latestLog.IsRead ?? false))
                        return BadRequest(new { success = false, message = "❌ شما هنوز تیک 'سند مشاهده شد' را نزده‌اید. لطفاً ابتدا سند را بررسی کنید." });
                }

                int statusId = validateByExpert ? 2 : 3;
                string statusName = validateByExpert ? "تأیید شده" : "رد شده";

                request.ValidateByExpert = validateByExpert;
                request.UpdatedAt = DateTime.UtcNow;
                request.UpdatedBy = User.Identity?.Name ?? userId.ToString();
                _context.Request.Update(request);

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

                await _actionLogger.Info(userId, "UpdateValidationStatus", $"{statusName} by expert");

                return Ok(new
                {
                    success = true,
                    message = $"درخواست با موفقیت {statusName}."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateValidationStatus");
                await _actionLogger.Error(0, "UpdateValidationStatus", $"Exception: {ex.Message}");
                return StatusCode(500, new { success = false, message = "⚠️ خطا در سرور." });
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
