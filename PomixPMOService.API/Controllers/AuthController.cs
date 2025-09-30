using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServicePomixPMO.API.Data;
using ServicePomixPMO.API.Models;
using ServicePomixPMO.API.Services;
using PomixPMOService.API.Models.ViewModels;
using System.Threading.Tasks;

namespace PomixPMOService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly PomixServiceContext _context;
        private readonly TokenService _tokenService;

        public AuthController(PomixServiceContext context, TokenService tokenService)
        {
            _context = context;
            _tokenService = tokenService;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginViewModel loginViewModel)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _context.Users
                .Include(u => u.Role) // join جدول Roles
                .FirstOrDefaultAsync(u => u.Username == loginViewModel.Username);

            if (user == null || !BCrypt.Net.BCrypt.Verify(loginViewModel.Password, user.PasswordHash))
                return Unauthorized("نام کاربری یا رمز عبور اشتباه است.");

            // بروزرسانی آخرین ورود 
            user.LastLogin = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // تولید توکن‌ها
            var tokens = await _tokenService.GenerateTokensAsync(user);

            // خروجی شامل اطلاعات کاربر و توکن‌ها
            return Ok(new
            {
                user.UserId,
                user.Username,
                user.Name,
                user.LastName,
                Role = new
                {
                    user.Role.RoleId,
                    user.Role.RoleName
                },
                Tokens = tokens // اضافه کردن توکن‌ها
            });
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequestViewModel model)
        {
            var refreshToken = await _tokenService.GetRefreshTokenAsync(model.RefreshToken);
            if (refreshToken == null)
                return Unauthorized("Refresh Token نامعتبر یا منقضی شده است.");

            var user = await _context.Users.FindAsync(refreshToken.UserId);
            if (user == null)
                return Unauthorized("کاربر یافت نشد.");

            object value = await _tokenService.RevokeRefreshTokenAsync(model.RefreshToken);
            var newTokens = await _tokenService.GenerateTokensAsync(user);

            await LogAction(user.UserId, "Refresh_Success", user.Username, "Token refreshed");

            return Ok(new
            {
                Message = "توکن با موفقیت تمدید شد",
                Tokens = newTokens
            });
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<UserViewModel>> Register(CreateUserViewModel viewModel)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (await _context.Users.AnyAsync(u => u.Username == viewModel.Username))
                return BadRequest("نام کاربری قبلاً ثبت شده است.");

            if (await _context.Users.AnyAsync(u => u.NationalId == viewModel.NationalId))
                return BadRequest("کد ملی قبلاً ثبت شده است.");

            var user = new User
            {
                NationalId = viewModel.NationalId,
                Username = viewModel.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(viewModel.Password),
                Name = viewModel.Name,
                LastName = viewModel.LastName,
                RoleId = viewModel.RoleId  // ← از ViewModel جدید باید RoleId بگیرید
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await LogAction(user.UserId, "Register_Success", user.Username, "User registered");

            var result = new UserViewModel
            {
                UserId = user.UserId,
                NationalId = user.NationalId,
                Username = user.Username,
                Name = user.Name,
                LastName = user.LastName,
                Role = user.Role.RoleName,   // ← اینجا فقط رشته می‌خوایم
                CreatedAt = user.CreatedAt,
                LastLogin = user.LastLogin,
                IsActive = user.IsActive
            };


            return CreatedAtAction(nameof(GetUsers), new { id = user.UserId }, result);
        }

        [HttpGet("GetUsers")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<UserViewModel>>> GetUsers()
        {
            var users = await _context.Users
                .Select(u => new UserViewModel
                {
                    UserId = u.UserId,
                    NationalId = u.NationalId,
                    Username = u.Username,
                    Name = u.Name,
                    LastName = u.LastName,
                    Role = u.Role.RoleName,
                    CreatedAt = u.CreatedAt,
                    LastLogin = u.LastLogin
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpPost("grant-access")]
        [Authorize(Policy = "CanManageAccess")]
        public async Task<IActionResult> GrantAccess([FromBody] GrantAccessViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null)
                return NotFound("کاربر یافت نشد.");

            var callerUserId = long.Parse(User.FindFirst("UserId")?.Value ?? "0");
            if (callerUserId == 0)
                return Unauthorized("کاربر شناسایی نشد.");

            var isAdmin = await _context.UserAccesses
                .AnyAsync(ua => ua.UserId == callerUserId && ua.Permission == "CanManageAccess");
            if (!isAdmin)
                return Forbid("شما دسترسی لازم برای مدیریت دسترسی‌ها را ندارید.");

            if (await _context.UserAccesses.AnyAsync(ua => ua.UserId == model.UserId && ua.Permission == model.Permission))
                return BadRequest("این دسترسی قبلاً برای کاربر ثبت شده است.");

            var userAccess = new UserAccess
            {
                UserId = (int)model.UserId,
                Permission = model.Permission
            };
            _context.UserAccesses.Add(userAccess);
            await _context.SaveChangesAsync();

            await LogAction(model.UserId, "GrantAccess_Success", user.Username, $"Permission {model.Permission} granted");
            return Ok($"دسترسی {model.Permission} به کاربر {user.Username} اعطا شد.");
        }

        [HttpDelete("revoke-access")]
        [Authorize(Policy = "CanManageAccess")]
        public async Task<IActionResult> RevokeAccess([FromBody] GrantAccessViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var callerUserId = long.Parse(User.FindFirst("UserId")?.Value ?? "0");
            if (callerUserId == 0)
                return Unauthorized("کاربر شناسایی نشد.");

            var isAdmin = await _context.UserAccesses
                .AnyAsync(ua => ua.UserId == callerUserId && ua.Permission == "CanManageAccess");
            if (!isAdmin)
                return Forbid("شما دسترسی لازم برای مدیریت دسترسی‌ها را ندارید.");

            var userAccess = await _context.UserAccesses
                .FirstOrDefaultAsync(ua => ua.UserId == model.UserId && ua.Permission == model.Permission);
            if (userAccess == null)
                return NotFound("دسترسی یافت نشد.");

            _context.UserAccesses.Remove(userAccess);
            await _context.SaveChangesAsync();

            var user = await _context.Users.FindAsync(model.UserId);
            await LogAction(model.UserId, "RevokeAccess_Success", user?.Username ?? "Unknown", $"Permission {model.Permission} revoked");
            return Ok($"دسترسی {model.Permission} از کاربر {user?.Username ?? "Unknown"} حذف شد.");
        }

        private async Task LogAction(long userId, string action, string? username, string result)
        {
            try
            {
                _context.UserLogs.Add(new UserLog
                {
                    UserId = (int?)userId,
                    Action = $"{action}: Username={username}, Result={result}",
                    ActionTime = DateTime.UtcNow,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers["User-Agent"].ToString()
                });
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving UserLog: {ex}");
            }
        }
    }

    public class RefreshRequestViewModel
    {
        public string RefreshToken { get; set; } = string.Empty;
    }
}