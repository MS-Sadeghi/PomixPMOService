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

        [HttpGet]
        public async Task<ActionResult<PaginatedResponse<RequestViewModel>>> GetAll(
            [FromQuery] PaginationParameters pagination = default!)
        {
            try
            {
                var query = _context.Request.AsQueryable();

                if (!string.IsNullOrEmpty(pagination.Search))
                {
                    var search = pagination.Search.ToLower();
                    query = query.Where(r =>
                        r.NationalId.ToLower().Contains(search) ||
                        r.MobileNumber.ToLower().Contains(search) ||
                        r.RequestCode.ToString().ToLower().Contains(search) ||
                        r.DocumentNumber.ToLower().Contains(search) ||
                        r.VerificationCode.ToLower().Contains(search)
                    );
                }

                var totalCount = await query.CountAsync();

                var items = await query
                    .OrderByDescending(r => r.CreatedAt) 
                    .Skip((pagination.Page - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
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

                var response = new PaginatedResponse<RequestViewModel>
                {
                    Items = items,
                    TotalCount = totalCount,
                    CurrentPage = pagination.Page,
                    PageSize = pagination.PageSize
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"خطا در دریافت درخواست‌ها: {ex.Message}");
            }
        }

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

        public class NewRequestViewModel
        {
            public string NationalId { get; set; } = string.Empty;  
            public string MobileNumber { get; set; } = string.Empty;   
            public string DocumentNumber { get; set; } = string.Empty;   
            public string VerificationCode { get; set; } = string.Empty; 
        }


    }
}
