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

        // POST: api/NewRequest
        [HttpPost]
        public async Task<ActionResult<RequestViewModel>> Create(CreateRequestViewModel viewModel)
        {
            var callerUserId = long.Parse(User.FindFirst("UserId")?.Value ?? "0");
            if (callerUserId == 0)
                return Unauthorized("کاربر شناسایی نشد.");

            var request = new Request
            {
                NationalId = viewModel.NationalId,
                MobileNumber = viewModel.MobileNumber,
                DocumentNumber = viewModel.DocumentNumber,
                VerificationCode = viewModel.VerificationCode,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = $"User_{callerUserId}"
            };

            _context.Request.Add(request);
            await _context.SaveChangesAsync();

            var result = new RequestViewModel
            {
                RequestId = request.RequestId,
                NationalId = request.NationalId,
                MobileNumber = request.MobileNumber,
                DocumentNumber = request.DocumentNumber,
                VerificationCode = request.VerificationCode,
                IsMatch = (bool)request.IsMatch,
                IsExist = request.IsExist,
                IsNationalIdInResponse = request.IsNationalIdInResponse,
                CreatedAt = request.CreatedAt,
                CreatedBy = request.CreatedBy
            };

            return CreatedAtAction(nameof(Get), new { id = request.RequestId }, result);
        }

        
    }
}
