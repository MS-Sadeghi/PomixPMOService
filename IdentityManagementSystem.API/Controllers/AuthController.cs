using IdentityManagementSystem.API.Data;
using IdentityManagementSystem.API.Models;
using IdentityManagementSystem.API.Models.ViewModels;
using IdentityManagementSystem.API.Services;
using IdentityManagementSystem.API.Services.SMS;
using IdentityManagementSystem.API.Services.SMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace IdentityManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IdentityManagementSystemContext _context;
        private readonly TokenService _tokenService;
        private readonly IMemoryCache _memoryCache;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IdentityManagementSystemContext context,
            TokenService tokenService,
            IMemoryCache memoryCache,
            IHostEnvironment hostEnvironment,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<AuthController> logger)
        {
            _context = context;
            _tokenService = tokenService;
            _memoryCache = memoryCache;
            _hostEnvironment = hostEnvironment;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
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
                return Unauthorized(new
                {
                    message = "نام کاربری یا رمز عبور اشتباه است."
                });

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

            // بررسی تکراری بودن نام کاربری و کد ملی
            if (await _context.Users.AnyAsync(u => u.Username == viewModel.Username))
                return BadRequest("نام کاربری قبلاً ثبت شده است.");

            if (await _context.Users.AnyAsync(u => u.NationalId == viewModel.NationalId))
                return BadRequest("کد ملی قبلاً ثبت شده است.");

            // ساخت کاربر جدید
            var user = new User
            {
                NationalId = viewModel.NationalId,
                Username = viewModel.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(viewModel.Password),
                Name = viewModel.Name,
                LastName = viewModel.LastName,
                MobileNumber = viewModel.MobileNumber,
                RoleId = viewModel.RoleId,
                CreatedAt = DateTime.Now,
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync(); // کاربر ثبت می‌شود

            // بارگذاری نقش با Include تا از NullReferenceException جلوگیری شود
            user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == user.UserId);

            if (user == null)
                return StatusCode(500, "خطا در ثبت کاربر.");

            // ثبت لاگ
            await LogAction(user.UserId, "Register_Success", user.Username, "User registered");

            // آماده‌سازی خروجی
            var result = new UserViewModel
            {
                UserId = user.UserId,
                NationalId = user.NationalId,
                Username = user.Username,
                Name = user.Name,
                LastName = user.LastName,
                Role = user.Role?.RoleName ?? "بدون نقش", // RoleName مستقیم از DB
                CreatedAt = user.CreatedAt,
                LastLogin = user.LastLogin,
                IsActive = user.IsActive,
                MobileNumber = user.MobileNumber
            };

            return CreatedAtAction(nameof(GetUsers), new { id = user.UserId }, result);
        }

        private bool IsAdmin()
        {
            var roleIdClaim = User.FindFirst("RoleId")?.Value;

            if (int.TryParse(roleIdClaim, out var roleId) && roleId == 3) return true;
            var roleName = User.FindFirst(ClaimTypes.Role)?.Value;
            return roleName == "ادمین";
        }


        [HttpGet("GetUsers")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<UserViewModel>>> GetUsers()
        {
            if (!IsAdmin())
            {
                return Forbid();
            }
            var users = await _context.Users
                .Include(u => u.Role) //  برای جلوگیری از NullReference در Role
                .Select(u => new UserViewModel
                {
                    UserId = u.UserId,
                    NationalId = u.NationalId,
                    Username = u.Username,
                    Name = u.Name,
                    LastName = u.LastName,
                    Role = u.Role != null ? u.Role.RoleName : "بدون نقش",
                    CreatedAt = u.CreatedAt,
                    LastLogin = u.LastLogin,
                    IsActive = u.IsActive,          //  اضافه شد تا وضعیت فعال/غیرفعال هم برگرده
                    MobileNumber = u.MobileNumber   //  اضافه شد برای نمایش شماره موبایل
                })
                .OrderByDescending(u => u.CreatedAt) //  اختیاری: کاربران جدیدتر اول بیایند
                .ToListAsync();

            return Ok(users);
        }

        [HttpPut("UpdateUser/{id}")]
        public async Task<IActionResult> UpdateUser(long id, [FromBody] UpdateUserRequest model)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound("کاربر یافت نشد.");

            // فقط فیلدهایی که مقدار دارند را به‌روزرسانی کن
            if (!string.IsNullOrEmpty(model.Name))
                user.Name = model.Name;

            if (!string.IsNullOrEmpty(model.LastName))
                user.LastName = model.LastName;

            if (!string.IsNullOrEmpty(model.Username))
                user.Username = model.Username;

            if (!string.IsNullOrEmpty(model.NationalId))
                user.NationalId = model.NationalId;

            if (!string.IsNullOrEmpty(model.MobileNumber))
                user.MobileNumber = model.MobileNumber;

            if (model.RoleId.HasValue && model.RoleId.Value > 0)
                user.RoleId = model.RoleId.Value;

            // اگر رمز جدید فرستاده شده بود، بروزرسانی کن
            if (!string.IsNullOrEmpty(model.Password))
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);

            await _context.SaveChangesAsync();
            await LogAction(user.UserId, "UpdateUser_Success", user.Username, "User updated successfully");

            return Ok(new { success = true, message = "اطلاعات کاربر با موفقیت بروزرسانی شد." });
        }

        // 🔴 Soft Delete (غیرفعال کردن کاربر)
        [HttpDelete("SoftDeleteUser/{id}")]
        public async Task<IActionResult> SoftDeleteUser(long id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound("کاربر یافت نشد.");

            if (!user.IsActive)
                return BadRequest("کاربر از قبل غیرفعال است.");

            user.IsActive = false;
            await _context.SaveChangesAsync();
            await LogAction(user.UserId, "SoftDeleteUser", user.Username, "User deactivated");

            return Ok("کاربر با موفقیت غیرفعال شد.");
        }

        // 🟢 فعال‌سازی مجدد کاربر
        [HttpPost("RestoreUser/{id}")]
        public async Task<IActionResult> RestoreUser(long id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound("کاربر یافت نشد.");

            if (user.IsActive)
                return BadRequest("کاربر از قبل فعال است.");

            user.IsActive = true;
            await _context.SaveChangesAsync();
            await LogAction(user.UserId, "RestoreUser", user.Username, "User restored");

            return Ok("کاربر با موفقیت فعال شد.");
        }

        // ⚫ حذف واقعی از دیتابیس (اختیاری)
        [HttpDelete("HardDeleteUser/{id}")]
        public async Task<IActionResult> HardDeleteUser(long id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound("کاربر یافت نشد.");

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            await LogAction(user.UserId, "HardDeleteUser", user.Username, "User permanently deleted");

            return Ok("کاربر به صورت دائم حذف شد.");
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

        [HttpPost("refresh/revoke")]
        [Authorize]
        public async Task<IActionResult> RevokeRefreshToken([FromBody] RefreshRequestViewModel model)
        {
            if (string.IsNullOrEmpty(model.RefreshToken))
                return BadRequest("Refresh Token ارائه نشده است.");

            var result = await _tokenService.RevokeRefreshTokenAsync(model.RefreshToken);
            return Ok(result);
        }

        [HttpPost("revoke-all")]
        [Authorize]
        public async Task<IActionResult> RevokeAllRefreshTokens([FromBody] RevokeAllRequestViewModel model)
        {
            var result = await _tokenService.RevokeAllRefreshTokensAsync(model.UserId);
            return Ok(result);
        }

        public class RevokeAllRequestViewModel
        {
            public long UserId { get; set; }
        }

        [HttpPost("ChangePassword")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
                return Unauthorized("کاربر شناسایی نشد.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
                return NotFound("کاربر یافت نشد.");

            if (!BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user.PasswordHash))
                return BadRequest("رمز عبور فعلی اشتباه است.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            await _context.SaveChangesAsync();

            await LogAction(userId, "ChangePassword_Success", user.Username, "Password changed successfully");
            return Ok("رمز عبور با موفقیت تغییر کرد.");
        }

        [HttpPost("forgot-password/start")]
        [AllowAnonymous]
        public async Task<IActionResult> StartForgotPassword([FromBody] ForgotPasswordStartViewModel model)
        {
            if (!ModelState.IsValid || !IsValidNationalId(model.NationalId) || !IsValidMobileNumber(model.MobileNumber))
                return BadRequest(new { message = "اطلاعات وارد شده معتبر نیست." });

            var cacheKey = BuildForgotPasswordIdentityKey(model.NationalId, model.MobileNumber);
            if (_memoryCache.TryGetValue<DateTimeOffset>($"{cacheKey}:cooldown", out _))
            {
                if (_memoryCache.TryGetValue<ForgotPasswordOtpState>(cacheKey, out var pendingState) &&
                    pendingState is not null &&
                    pendingState.ExpiresAt > DateTimeOffset.UtcNow)
                {
                    var remainingSeconds = Math.Max(1, (int)(pendingState.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds);
                    return Ok(new { message = "کد تأیید قبلاً ارسال شده است.", expiresInSeconds = remainingSeconds });
                }

                return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "برای ارسال مجدد کد کمی صبر کنید." });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.NationalId == model.NationalId &&
                u.MobileNumber == model.MobileNumber &&
                u.IsActive);

            _memoryCache.Set($"{cacheKey}:cooldown", DateTimeOffset.UtcNow, TimeSpan.FromSeconds(60));

            if (user != null)
            {
                var code = RandomNumberGenerator.GetInt32(10000, 100000).ToString();
                if (_hostEnvironment.IsDevelopment())
                {
                    var debugMessage = $"ForgotPassword OTP for {model.NationalId}/{model.MobileNumber}: {code}";
                    Console.WriteLine(debugMessage);
                    Debug.WriteLine(debugMessage);
                }

                var smsText = $"کد تأیید بازنشانی رمز عبور سامانه جامع گزارش‌گیری ترددها: {code}\nاعتبار: ۵ دقیقه";
                var smsModel = new SendSmsViewModel
                {
                    Mobile = user.MobileNumber!,
                    Text = smsText,
                    LogText = "ارسال کد تأیید بازنشانی رمز عبور",
                    Area = "Security",
                    Controller = "Account",
                    Action = "ForgotPassword",
                    UserId = user.UserId
                };

                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = Request.Headers["User-Agent"].ToString();

                _memoryCache.Set(cacheKey, new ForgotPasswordOtpState
                {
                    UserId = user.UserId,
                    CodeHash = BCrypt.Net.BCrypt.HashPassword(code),
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                    FailedAttempts = 0
                }, TimeSpan.FromMinutes(5));

                QueueForgotPasswordSms(smsModel, user.UserId, user.Username, ipAddress, userAgent);
                QueueUserLog(user.UserId, "ForgotPassword_Code_Generated", user.Username, "Password reset code generated", ipAddress, userAgent);
            }
            else
            {
                return BadRequest("کد ملی یا شماره موبایل اشتباه است");
            }

            return Ok(new { message = "در صورت تطابق اطلاعات، کد تأیید ارسال می‌شود.", expiresInSeconds = 300 });
        }

        [HttpPost("forgot-password/verify")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyForgotPasswordCode([FromBody] ForgotPasswordVerifyViewModel model)
        {
            if (!ModelState.IsValid || !IsValidNationalId(model.NationalId) || !IsValidMobileNumber(model.MobileNumber) || !Regex.IsMatch(model.Code ?? "", "^\\d{5}$"))
                return BadRequest(new { message = "کد تأیید معتبر نیست." });

            var cacheKey = BuildForgotPasswordIdentityKey(model.NationalId, model.MobileNumber);
            if (!_memoryCache.TryGetValue<ForgotPasswordOtpState>(cacheKey, out var state) || state.ExpiresAt < DateTimeOffset.UtcNow)
                return BadRequest(new { message = "کد تأیید نامعتبر یا منقضی شده است." });

            if (state.FailedAttempts >= 5)
            {
                _memoryCache.Remove(cacheKey);
                return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "تعداد تلاش‌ها بیش از حد مجاز است. دوباره درخواست کد دهید." });
            }

            if (!BCrypt.Net.BCrypt.Verify(model.Code, state.CodeHash))
            {
                state.FailedAttempts++;
                _memoryCache.Set(cacheKey, state, state.ExpiresAt);
                return BadRequest(new { message = "کد تأیید نامعتبر یا منقضی شده است." });
            }

            var resetToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            _memoryCache.Set(BuildForgotPasswordResetKey(resetToken), state.UserId, TimeSpan.FromMinutes(10));
            _memoryCache.Remove(cacheKey);

            var user = await _context.Users.FindAsync(state.UserId);
            await LogAction(state.UserId, "ForgotPassword_Code_Verified", user?.Username, "Password reset code verified");

            return Ok(new { message = "کد تأیید شد.", resetToken });
        }

        [HttpPost("forgot-password/reset")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetForgotPassword([FromBody] ForgotPasswordResetViewModel model)
        {
            if (!ModelState.IsValid || model.NewPassword != model.ConfirmNewPassword || !IsStrongPassword(model.NewPassword))
                return BadRequest(new { message = "رمز عبور جدید معتبر نیست یا با تأیید آن یکسان نیست." });

            var resetKey = BuildForgotPasswordResetKey(model.ResetToken);
            if (!_memoryCache.TryGetValue<long>(resetKey, out var userId))
                return BadRequest(new { message = "نشست بازنشانی نامعتبر یا منقضی شده است." });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive);
            if (user == null)
                return BadRequest(new { message = "نشست بازنشانی نامعتبر یا منقضی شده است." });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            await _context.SaveChangesAsync();
            _memoryCache.Remove(resetKey);

            await LogAction(user.UserId, "ForgotPassword_Reset_Success", user.Username, "Password reset successfully");
            return Ok(new { message = "رمز عبور با موفقیت تغییر کرد." });
        }

        [HttpGet("GetCurrentUser")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                var userIdClaim = User.FindFirst("UserId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized("کاربر شناسایی نشد.");
                }

                var user = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user == null)
                {
                    return NotFound("کاربر یافت نشد.");
                }

                var userInfo = new
                {
                    user.UserId,
                    user.Username,
                    user.Name,
                    user.LastName,
                    Role = user.Role?.RoleName ?? "بدون نقش"
                };

                return Ok(userInfo);
            }
            catch (Exception ex)
            {
                await LogAction(0, "GetCurrentUser_Error", null, ex.Message);
                return StatusCode(500, new { message = "خطای غیرمنتظره‌ای رخ داد. لطفاً بعداً دوباره تلاش کنید." });
            }
        }

        [HttpGet("GetRoles")]
        public async Task<IActionResult> GetRoles()
        {
            try
            {
                var roles = await _context.Roles
                    .Select(r => new
                    {
                        roleId = r.RoleId,
                        roleName = r.RoleName
                    })
                    .ToListAsync();

                return Ok(roles);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "خطای غیرمنتظره‌ای رخ داد. لطفاً بعداً دوباره تلاش کنید." });
            }
        }

        private async Task LogAction(long userId, string action, string? username, string result)
        {
            if (userId <= 0)
                return;

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
            catch (Exception)
            {
            }
        }

        private void QueueForgotPasswordSms(SendSmsViewModel smsModel, long userId, string username, string? ipAddress, string userAgent)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var sendSmsService = scope.ServiceProvider.GetRequiredService<SendSmsService>();
                    var smsSent = await sendSmsService.SendSmsAsync(smsModel);

                    if (!smsSent)
                    {
                        var context = scope.ServiceProvider.GetRequiredService<IdentityManagementSystemContext>();
                        context.UserLogs.Add(new UserLog
                        {
                            UserId = userId,
                            Action = $"ForgotPassword_Code_Send_Failed: Username={username}, Result=Password reset code SMS failed",
                            ActionTime = DateTime.UtcNow,
                            IpAddress = ipAddress,
                            UserAgent = userAgent
                        });
                        await context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Forgot password SMS background send failed for user {UserId}", userId);
                }
            });
        }

        private void QueueUserLog(long userId, string action, string? username, string result, string? ipAddress, string userAgent)
        {
            if (userId <= 0)
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<IdentityManagementSystemContext>();
                    context.UserLogs.Add(new UserLog
                    {
                        UserId = userId,
                        Action = $"{action}: Username={username}, Result={result}",
                        ActionTime = DateTime.UtcNow,
                        IpAddress = ipAddress,
                        UserAgent = userAgent
                    });
                    await context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Forgot password background log failed for user {UserId}", userId);
                }
            });
        }

        private static string BuildForgotPasswordIdentityKey(string nationalId, string mobileNumber)
            => $"forgot-password:{nationalId}:{mobileNumber}";

        private static string BuildForgotPasswordResetKey(string resetToken)
            => $"forgot-password:reset:{resetToken}";

        private static bool IsValidNationalId(string? nationalId)
            => !string.IsNullOrWhiteSpace(nationalId) && Regex.IsMatch(nationalId, "^\\d{10}$");

        private static bool IsValidMobileNumber(string? mobileNumber)
            => !string.IsNullOrWhiteSpace(mobileNumber) && Regex.IsMatch(mobileNumber, "^09\\d{9}$");

        private static bool IsStrongPassword(string? password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                return false;

            return password.Any(char.IsLower) &&
                   password.Any(char.IsUpper) &&
                   password.Any(char.IsDigit) &&
                   password.Any(ch => !char.IsLetterOrDigit(ch));
        }
    }

    public class RefreshRequestViewModel
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class ChangePasswordViewModel
    {
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
        public string? ConfirmNewPassword { get; set; }
    }

    public class ForgotPasswordStartViewModel
    {
        [Required]
        [StringLength(10, MinimumLength = 10)]
        public string NationalId { get; set; } = string.Empty;

        [Required]
        [StringLength(11, MinimumLength = 11)]
        public string MobileNumber { get; set; } = string.Empty;
    }

    public class ForgotPasswordVerifyViewModel : ForgotPasswordStartViewModel
    {
        [Required]
        [StringLength(5, MinimumLength = 5)]
        public string Code { get; set; } = string.Empty;
    }

    public class ForgotPasswordResetViewModel
    {
        [Required]
        public string ResetToken { get; set; } = string.Empty;

        [Required]
        [StringLength(255, MinimumLength = 8)]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [Compare(nameof(NewPassword))]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }

    internal sealed class ForgotPasswordOtpState
    {
        public long UserId { get; set; }
        public string CodeHash { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
        public int FailedAttempts { get; set; }
    }
}
