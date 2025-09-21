using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PomixPMOService.API.Models.ViewModels;
using ServicePomixPMO.API.Data;
using ServicePomixPMO.API.Models;
using System.Security.Claims;

namespace ServiceUIPomixPMO.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly PomixServiceContext _context;

        public AuthController(PomixServiceContext context)
        {
            _context = context;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginViewModel loginViewModel)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == loginViewModel.Username);

            if (user == null || !BCrypt.Net.BCrypt.Verify(loginViewModel.Password, user.PasswordHash))
                return Unauthorized("نام کاربری یا رمز عبور اشتباه است.");

            user.LastLogin = DateTime.UtcNow;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // ساخت ClaimsIdentity
            var claims = new List<Claim>
            {
                  new Claim("UserId", user.UserId.ToString()),
                  new Claim("Username", user.Username)
            };

            var identity = new ClaimsIdentity(claims, "Custom");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("CustomScheme", principal);

            return Ok(new
            {
                Message = "ورود موفقیت آمیز بود",
                User = new
                {
                    user.UserId,
                    user.Username,
                    user.Name,
                    user.LastName,
                    user.Role
                }
            });
        }


        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<UserViewModel>> Register(CreateUserViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (await _context.Users.AnyAsync(u => u.Username == viewModel.Username))
            {
                return BadRequest("نام کاربری قبلاً ثبت شده است.");
            }
            if (await _context.Users.AnyAsync(u => u.NationalId == viewModel.NationalId))
            {
                return BadRequest("کد ملی قبلاً ثبت شده است.");
            }

            var user = new User
            {
                NationalId = viewModel.NationalId,
                Username = viewModel.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(viewModel.Password),
                Name = viewModel.Name,
                LastName = viewModel.LastName,
                Role = viewModel.Role
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
                Role = user.Role,
                CreatedAt = user.CreatedAt,
                LastLogin = user.LastLogin,
                IsActive = user.IsActive
            };
            return CreatedAtAction(nameof(GetUser), new { id = user.UserId }, result);
        }

        [HttpGet("users")]
        public async Task<ActionResult<IEnumerable<UserViewModel>>> GetUsers()
        {
            return await _context.Users
                .Select(u => new UserViewModel
                {
                    UserId = u.UserId,
                    NationalId = u.NationalId,
                    Username = u.Username,
                    Name = u.Name,
                    LastName = u.LastName,
                    Role = u.Role,
                    CreatedAt = u.CreatedAt,
                    LastLogin = u.LastLogin,
                    IsActive = u.IsActive
                })
                .ToListAsync();
        }

        [HttpGet("users/{id}")]
        public async Task<ActionResult<UserViewModel>> GetUser(long id)
        {
            var user = await _context.Users
                .Select(u => new UserViewModel
                {
                    UserId = u.UserId,
                    NationalId = u.NationalId,
                    Username = u.Username,
                    Name = u.Name,
                    LastName = u.LastName,
                    Role = u.Role,
                    CreatedAt = u.CreatedAt,
                    LastLogin = u.LastLogin,
                    IsActive = u.IsActive
                })
                .FirstOrDefaultAsync(u => u.UserId == id);
            if (user == null)
                return NotFound();
            return user;
        }

        [HttpPost("grant-access")]
        public async Task<IActionResult> GrantAccess([FromBody] GrantAccessViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null)
            {
                return NotFound("کاربر یافت نشد.");
            }

            var callerUserIdObj = HttpContext.Items["UserId"];
            if (callerUserIdObj == null || !long.TryParse(callerUserIdObj.ToString(), out long callerUserId))
            {
                return Unauthorized("کاربر شناسایی نشد.");
            }

            var isAdmin = await _context.UserAccesses
                .AnyAsync(ua => ua.UserId == callerUserId && ua.Permission == "CanManageAccess");
            if (!isAdmin)
            {
                return Forbid("شما دسترسی لازم برای مدیریت دسترسی‌ها را ندارید.");
            }

            if (await _context.UserAccesses.AnyAsync(ua => ua.UserId == model.UserId && ua.Permission == model.Permission))
            {
                return BadRequest("این دسترسی قبلاً برای کاربر ثبت شده است.");
            }

            var userAccess = new UserAccess
            {
                UserId = model.UserId,
                Permission = model.Permission
            };
            _context.UserAccesses.Add(userAccess);
            await _context.SaveChangesAsync();

            await LogAction(model.UserId, "GrantAccess_Success", user.Username, $"Permission {model.Permission} granted");
            return Ok($"دسترسی {model.Permission} به کاربر {user.Username} اعطا شد.");
        }

        [HttpDelete("revoke-access")]
        public async Task<IActionResult> RevokeAccess([FromBody] GrantAccessViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var callerUserIdObj = HttpContext.Items["UserId"];
            if (callerUserIdObj == null || !long.TryParse(callerUserIdObj.ToString(), out long callerUserId))
            {
                return Unauthorized("کاربر شناسایی نشد.");
            }

            var isAdmin = await _context.UserAccesses
                .AnyAsync(ua => ua.UserId == callerUserId && ua.Permission == "CanManageAccess");
            if (!isAdmin)
            {
                return Forbid("شما دسترسی لازم برای مدیریت دسترسی‌ها را ندارید.");
            }

            var userAccess = await _context.UserAccesses
                .FirstOrDefaultAsync(ua => ua.UserId == model.UserId && ua.Permission == model.Permission);
            if (userAccess == null)
            {
                return NotFound("دسترسی یافت نشد.");
            }

            _context.UserAccesses.Remove(userAccess);
            await _context.SaveChangesAsync();

            var user = await _context.Users.FindAsync(model.UserId);
            await LogAction(model.UserId, "RevokeAccess_Success", user?.Username ?? "Unknown", $"Permission {model.Permission} revoked");
            return Ok($"دسترسی {model.Permission} از کاربر {user?.Username ?? "Unknown"} حذف شد.");
        }

        private async Task LogAction(long userId, string action, string username, string result)
        {
            try
            {
                _context.UserLogs.Add(new UserLog
                {
                    UserId = userId,
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
}