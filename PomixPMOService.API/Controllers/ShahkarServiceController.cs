using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ServicePomixPMO;
using ServicePomixPMO.API.Data;
using ServicePomixPMO.API.Models;
using System.Text;
using System.Text.Json;
using JsonException = Newtonsoft.Json.JsonException;

namespace PomixPMOService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShahkarServiceController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly PomixServiceContext _context;
        private readonly ShahkarServiceOptions _options;
        private readonly IMemoryCache _cache;
        private readonly string _providerCode = "0785";

        public ShahkarServiceController(
            HttpClient httpClient,
            PomixServiceContext context,
            IOptions<ShahkarServiceOptions> options,
            IMemoryCache cache)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));

            if (string.IsNullOrEmpty(_options.BaseUrl) || _options.Credential == null ||
                string.IsNullOrEmpty(_options.Credential.Code) || string.IsNullOrEmpty(_options.Credential.Password))
            {
                throw new InvalidOperationException("تنظیمات سرویس شاهکار ناقص است.");
            }
        }

        [HttpPost("CheckMobileNationalCode")]
        [PermissionAuthorize("CanAccessShahkar")]
        public async Task<IActionResult> CheckMobileNationalCode([FromBody] MobileCheckViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Validate format
            if (model.NationalId.Length != 10 || !model.NationalId.All(char.IsDigit) ||
                model.MobileNumber.Length != 11 || !model.MobileNumber.All(char.IsDigit))
                return BadRequest("فرمت کد ملی یا شماره موبایل نامعتبر است.");

            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out long userId))
                return Unauthorized("کاربر شناسایی نشد.");

            // Limit requests
            if (!await CanMakeShahkarRequest(userId))
                return BadRequest("محدودیت تعداد درخواست‌ها: لطفاً بعداً تلاش کنید.");

            // Cache
            string cacheKey = $"Shahkar_{model.NationalId}_{model.MobileNumber}";
            if (_cache.TryGetValue(cacheKey, out var cachedResult))
                return Ok(cachedResult);

            // Generate unique request code
            var requestCode = GenerateRequestId();

            var requestBody = new
            {
                credential = new
                {
                    code = _options.Credential.Code,
                    password = _options.Credential.Password
                },
                parameters = new[]
                {
            new { parameterName = "serviceType", parameterValue = "2" },
            new { parameterName = "identificationType", parameterValue = "0" },
            new { parameterName = "identificationNo", parameterValue = model.NationalId },
            new { parameterName = "requestId", parameterValue = requestCode },
            new { parameterName = "serviceNumber", parameterValue = model.MobileNumber }
        },
                service = "gsb-itoshahkar"
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(_options.BaseUrl, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, $"خطا در ارتباط با سرویس شاهکار: {responseString}");

                var shahkarResponse = JsonConvert.DeserializeObject<ShahkarResponse>(responseString);
                var isMatch = shahkarResponse?.ResponseText?.Contains("\"response\":200") ?? false;

                // ذخیره لاگ فقط در ShahkarLog
                var log = new ShahkarLog
                {
                    NationalId = model.NationalId,
                    MobileNumber = model.MobileNumber,
                    RequestCode = requestCode,
                    ExpertId = userId,
                    IsMatch = isMatch,
                    ResponseText = responseString,
                    CreatedAt = DateTime.UtcNow
                };
                _context.ShahkarLog.Add(log);

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException dbEx)
                {
                    var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                    return StatusCode(500, $"خطای دیتابیس: {innerMessage}");
                }

                var result = new
                {
                    model.NationalId,
                    model.MobileNumber,
                    RequestCode = requestCode,
                    IsMatch = isMatch,
                    Message = isMatch ? "کد ملی و شماره موبایل همخوانی دارند." : "کد ملی و شماره موبایل همخوانی ندارند."
                };

                _cache.Set(cacheKey, result, TimeSpan.FromHours(24));
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"خطای سرور: {ex.Message}");
            }
        }

        [HttpPost("VerifyDocument")]
        [PermissionAuthorize("CanAccessShahkar")]
        public async Task<IActionResult> VerifyDocument([FromBody] DocumentVerifyViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Validate format
            if (string.IsNullOrEmpty(model.DocumentNumber) || !model.DocumentNumber.All(char.IsDigit) ||
                string.IsNullOrEmpty(model.VerificationCode) || !model.VerificationCode.All(char.IsDigit))
                return BadRequest("فرمت شناسه یکتای سند یا رمز تصدیق نامعتبر است.");

            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out long userId))
                return Unauthorized("کاربر شناسایی نشد.");

            // Limit requests
            if (!await CanMakeShahkarRequest(userId))
                return BadRequest("محدودیت تعداد درخواست‌ها: لطفاً بعداً تلاش کنید.");

            // Cache
            string cacheKey = $"VerifyDoc_{model.DocumentNumber}_{model.VerificationCode}";
            if (_cache.TryGetValue(cacheKey, out var cachedResult))
                return Ok(cachedResult);

            // Generate unique request code
            var requestCode = Guid.NewGuid().ToString();

            var requestBody = new
            {
                credential = new
                {
                    code = _options.Credential.Code,
                    password = _options.Credential.Password
                },
                parameters = new[]
                {
            new { parameterName = "NationalRegisterNo", parameterValue = model.DocumentNumber },
            new { parameterName = "SecretNo", parameterValue = model.VerificationCode }
        },
                service = "gsb-Approval2-GetData"
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var requestDate = DateTime.UtcNow;
                var response = await _httpClient.PostAsync(_options.BaseUrl, content);
                var responseString = await response.Content.ReadAsStringAsync();
                var responseDate = DateTime.UtcNow;

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, $"خطا در ارتباط با سرویس: {responseString}");

                JsonDocument jsonDoc;
                try
                {
                    jsonDoc = JsonDocument.Parse(responseString);
                }
                catch (JsonException ex)
                {
                    return StatusCode(500, $"خطا: پاسخ سرویس JSON معتبر نیست - {ex.Message}");
                }

                if (!jsonDoc.RootElement.TryGetProperty("result", out var resultElement) ||
                    !resultElement.TryGetProperty("data", out var data))
                {
                    return StatusCode(500, $"خطا: ساختار پاسخ سرویس نامعتبر است - کلید 'result' یا 'data' یافت نشد.");
                }

                // تابع کمکی برای گرفتن مقادیر امن
                string GetString(string key) => data.TryGetProperty(key, out var val) && val.ValueKind != JsonValueKind.Null ? val.GetString() ?? "" : "";
                bool GetBool(string key) => data.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.True;

                var log = new VerifyDocLog
                {
                    DocumentNumber = model.DocumentNumber ?? "",
                    VerificationCode = model.VerificationCode ?? "",
                    DocType = GetString("DocType"),
                    DocType_Code = GetString("DocType_code"),
                    HasPermission = GetBool("HasPermission"),
                    ExistDoc = GetBool("ExistDoc"),
                    Desc = GetString("Desc"),
                    ScriptoriumName = GetString("ScriptoriumName"),
                    SignGetterTitle = GetString("SignGetterTitle"),
                    SignSubject = GetString("SignSubject"),
                    DocDate = GetString("DocDate"),
                    CaseClasifyNo = GetString("CaseClasifyNo"),
                    ImpotrtantAnnexText = GetString("ImpotrtantAnnexText"),
                    ADVOCACYENDDATE = GetString("ADVOCACYENDDATE"),
                    LstFindPersonInQuery = data.TryGetProperty("lstFindPersonInQuery", out var list) && list.ValueKind != JsonValueKind.Null ? list.GetRawText() : null,
                    ResponseText = responseString,
                    ExpertId = userId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.VerifyDocLog.Add(log);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException dbEx)
                {
                    var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                    return StatusCode(500, $"خطای دیتابیس: {innerMessage}");
                }

                var result = new
                {
                    isSuccessful = true,
                    error = 0,
                    errorDescription = "",
                    UsedQuotaStats = new
                    {
                        hourlyUsed = 10, // باید از منبع واقعی دریافت شود
                        dailyUsed = 11,
                        monthlyUsed = 11
                    },
                    description = "",
                    responseStatusCode = (int)response.StatusCode,
                    responseText = responseString,
                    requestDate = requestDate.ToString("o"),
                    responseDate = responseDate.ToString("o"),
                    responseHeaders = new
                    {
                        ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json;charset=UTF-8"
                    },
                    cookies = (object)null,
                    digitalSignature = "sample_signature", // باید از سرور واقعی دریافت شود
                    requestId = requestCode
                };

                _cache.Set(cacheKey, result, TimeSpan.FromHours(24));
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"خطای سرور: {ex.Message}");
            }
        }


        private string GenerateRequestId()
        {
            var now = DateTime.Now;

            // بخش تاریخ و زمان: yyyyMMddHHmmss
            string dateTimePart = now.ToString("yyyyMMddHHmmss");

            // میکروثانیه: از Ticks استفاده می‌کنیم و 6 رقم آخر رو برمی‌داریم
            string microseconds = (now.Ticks % 1000000).ToString("D6");

            // Combine provider code + datetime + microseconds
            return $"{_providerCode}{dateTimePart}{microseconds}";
        }

        private async Task<bool> CanMakeShahkarRequest(long expertId)
        {
            var lastHour = DateTime.UtcNow.AddHours(-1);
            var requestCount = await _context.Request
                .CountAsync(r => r.ExpertId == expertId && r.CreatedAt > lastHour);
            return requestCount < 10;
        }
    }

    public class MobileCheckViewModel
    {
        public string NationalId { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
    }

    public class DocumentVerifyViewModel
    {
        public string DocumentNumber { get; set; }    // NationalRegisterNo
        public string VerificationCode { get; set; }  // SecretNo
    }


    public class ShahkarServiceOptions
    {
        public string? BaseUrl { get; set; }
        public Credential? Credential { get; set; }
    }

    public class Credential
    {
        public string? Code { get; set; }
        public string? Password { get; set; }
    }

    public class ShahkarResponse
    {
        public bool IsSuccessful { get; set; }
        public int Error { get; set; }
        public string? ErrorDescription { get; set; }
        public string? ResponseText { get; set; }
        public int ResponseStatusCode { get; set; }
        public UsedQuotaStats? UsedQuotaStats { get; set; }
        public string? Description { get; set; }
        public string? RequestDate { get; set; }
        public string? ResponseDate { get; set; }
        public ResponseHeaders? ResponseHeaders { get; set; }
        public string? DigitalSignature { get; set; }
        public string? RequestId { get; set; }
    }

    public class UsedQuotaStats
    {
        public int HourlyUsed { get; set; }
        public int DailyUsed { get; set; }
        public int MonthlyUsed { get; set; }
    }

    public class ResponseHeaders
    {
        public string? ContentType { get; set; }
    }
}