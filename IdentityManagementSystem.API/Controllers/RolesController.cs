using IdentityManagementSystem.API.Data;
using IdentityManagementSystem.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace IdentityManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "AdminOnly")]
    public class RolesController : ControllerBase
    {
        // نقش ادمین به نام آن در کد وابسته است؛ نام و وضعیتش قابل تغییر نیست
        private const int AdminRoleId = 3;

        private readonly IdentityManagementSystemContext _context;

        public RolesController(IdentityManagementSystemContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var roles = await _context.Roles
                .OrderBy(r => r.RoleId)
                .Select(r => new
                {
                    r.RoleId,
                    r.RoleName,
                    r.IsActive,
                    r.CreatedAt,
                    r.UpdatedAt,
                    UserCount = _context.UserRoles.Count(ur => ur.RoleId == r.RoleId),
                    IsProtected = r.RoleId == AdminRoleId
                })
                .ToListAsync();

            return Ok(roles);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] RoleRequest model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { message = "نام نقش الزامی است و نباید بیش از ۲۰۰ کاراکتر باشد." });

            var name = model.RoleName!.Trim();
            if (await _context.Roles.AnyAsync(r => r.RoleName == name))
                return Conflict(new { message = "نقشی با این نام قبلاً ثبت شده است." });

            var role = new Role
            {
                RoleName = name,
                IsActive = model.IsActive,
                CreatedAt = DateTime.Now
            };
            _context.Roles.Add(role);
            await _context.SaveChangesAsync();

            return Created($"api/Roles/{role.RoleId}", new { role.RoleId, role.RoleName, role.IsActive });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] RoleRequest model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { message = "نام نقش الزامی است و نباید بیش از ۲۰۰ کاراکتر باشد." });

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleId == id);
            if (role == null)
                return NotFound(new { message = "نقش یافت نشد." });

            var name = model.RoleName!.Trim();

            if (id == AdminRoleId && (name != role.RoleName || !model.IsActive))
                return BadRequest(new { message = "نام و وضعیت نقش ادمین قابل تغییر نیست." });

            if (await _context.Roles.AnyAsync(r => r.RoleName == name && r.RoleId != id))
                return Conflict(new { message = "نقشی با این نام قبلاً ثبت شده است." });

            role.RoleName = name;
            role.IsActive = model.IsActive;
            role.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return Ok(new { role.RoleId, role.RoleName, role.IsActive });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (id == AdminRoleId)
                return BadRequest(new { message = "نقش ادمین قابل حذف نیست." });

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleId == id);
            if (role == null)
                return NotFound(new { message = "نقش یافت نشد." });

            if (await _context.UserRoles.AnyAsync(ur => ur.RoleId == id))
                return Conflict(new { message = "این نقش به کاربرانی اختصاص داده شده و قابل حذف نیست. در صورت نیاز آن را غیرفعال کنید." });

            try
            {
                _context.Roles.Remove(role);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // نقش در جدول‌های دیگر (دسترسی‌ها، کارتابل، مراحل) استفاده شده است
                return Conflict(new { message = "این نقش در بخش‌های دیگر سامانه استفاده شده و قابل حذف نیست. در صورت نیاز آن را غیرفعال کنید." });
            }

            return Ok(new { message = "نقش حذف شد." });
        }
    }

    public class RoleRequest
    {
        [Required]
        [StringLength(200, MinimumLength = 1)]
        public string? RoleName { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
