using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PomixPMOService.API.Models.ViewModels;
using ServicePomixPMO.API.Data;
using ServicePomixPMO.API.Models;

namespace ServicePomixPMO.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CartablesController : ControllerBase
    {
        private readonly PomixServiceContext _context;

        public CartablesController(PomixServiceContext context)
        {
            _context = context;
        }

        // GET: api/cartables/user/5
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<CartableItemViewModel>>> GetCartableItems(long userId)
        {
            var cartableItems = await _context.CartableItems
                .Include(ci => ci.Request)
                .Include(ci => ci.AssignedToUser)
                .Include(ci => ci.Cartable)
                .Where(ci => ci.Cartable != null
                              && ci.Cartable.UserId == userId
                              && (ci.AssignedTo == userId || ci.AssignedTo == null)
                              && ci.Request != null)

                .Select(ci => new CartableItemViewModel
                {
                    ItemId = ci.ItemId,
                    RequestId = ci.RequestId,
                    NationalId = ci.Request!.NationalId, // الان کامپایلر می‌دونه null نیست
                    DocumentNumber = ci.Request!.DocumentNumber,
                    VerificationCode = ci.Request!.VerificationCode,
                    IdentityVerified = ci.Request!.IdentityVerified,
                    DocumentVerified = ci.Request!.DocumentVerified,
                    DocumentMatch = ci.Request!.DocumentMatch,
                    TextApproved = ci.Request!.TextApproved,
                    RequestStatus = ci.Request!.RequestStatus,
                    AssignedTo = ci.AssignedTo,
                    AssignedToName = ci.AssignedToUser != null ? $"{ci.AssignedToUser.Name} {ci.AssignedToUser.LastName}" : null,
                    AssignedAt = ci.AssignedAt,
                    ViewedAt = ci.ViewedAt,
                    Status = ci.Status
                })
                .ToListAsync();

            return Ok(cartableItems);
        }


        // POST: api/cartables/assign
        [HttpPost("assign")]
        public async Task<IActionResult> AssignCartableItem(AssignCartableItemViewModel viewModel)
        {
            var cartableItem = await _context.CartableItems.FindAsync(viewModel.ItemId);
            if (cartableItem == null)
                return NotFound();

            cartableItem.AssignedTo = viewModel.AssignedTo;
            cartableItem.AssignedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // ثبت لاگ
            _context.RequestLogs.Add(new RequestLog
            {
                RequestId = cartableItem.RequestId,
                UserId = viewModel.AssignedTo,
                Action = "Assign",
                Details = $"آیتم کارتابل به کاربر {viewModel.AssignedTo} تخصیص یافت"
            });
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}