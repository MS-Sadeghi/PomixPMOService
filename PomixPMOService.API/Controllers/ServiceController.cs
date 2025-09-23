using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using ServicePomixPMO;
using ServicePomixPMO.API.Data;
using System.Text;
using System.Text.Json;
using JsonException = System.Text.Json.JsonException;

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
        private readonly ILogger<ShahkarServiceController> _logger;
        private readonly string _providerCode = "0785";
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy; // Fixed type to AsyncRetryPolicy<HttpResponseMessage>

        public ShahkarServiceController(
            HttpClient httpClient,
            PomixServiceContext context,
            IOptions<ShahkarServiceOptions> options,
            IMemoryCache cache,
            ILogger<ShahkarServiceController> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (string.IsNullOrEmpty(_options.BaseUrl) || _options.Credential == null ||
                string.IsNullOrEmpty(_options.Credential.Code) || string.IsNullOrEmpty(_options.Credential.Password))
            {
                _logger.LogError("Shahkar service configuration is incomplete.");
                throw new InvalidOperationException("تنظیمات سرویس شاهکار ناقص است.");
            }

            _retryPolicy = Policy
                .Handle<HttpRequestException>()
                .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (result, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning("Retry {RetryCount} after {TimeSpan} seconds due to {Reason}",
                            retryCount, timeSpan.TotalSeconds, result.Exception?.Message ?? "non-success status code");
                    });
        }

        [HttpPost("CheckMobileNationalCode")]
        [PermissionAuthorize("CanAccessShahkar")]
        public async Task<IActionResult> CheckMobileNationalCode([FromBody] MobileCheckViewModel model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for CheckMobileNationalCode: {Errors}",
                    string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return BadRequest(ModelState);
            }

            // Validate format
            if (model.NationalId.Length != 10 || !model.NationalId.All(char.IsDigit) ||
                model.MobileNumber.Length != 11 || !model.MobileNumber.All(char.IsDigit))
            {
                _logger.LogWarning("Invalid NationalId or MobileNumber format: NationalId={NationalId}, MobileNumber={MobileNumber}",
                    model.NationalId, model.MobileNumber);
                return BadRequest("فرمت کد ملی یا شماره موبایل نامعتبر است.");
            }

            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out long userId))
            {
                _logger.LogWarning("User ID not found or invalid in claims");
                return Unauthorized("کاربر شناسایی نشد.");
            }

            // Limit requests
            if (!await CanMakeShahkarRequest(userId))
            {
                _logger.LogWarning("Request limit exceeded for user {UserId}", userId);
                return BadRequest("محدودیت تعداد درخواست‌ها: لطفاً بعداً تلاش کنید.");
            }

            // Cache
            string cacheKey = $"Shahkar_{model.NationalId}_{model.MobileNumber}";
            if (_cache.TryGetValue(cacheKey, out var cachedResult))
            {
                _logger.LogInformation("Cache hit for {CacheKey}", cacheKey);
                return Ok(cachedResult);
            }

            // Generate unique request code
            var requestCode = GenerateRequestId();
            _logger.LogInformation("Generated request ID {RequestId} for NationalId {NationalId}", requestCode, model.NationalId);

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
                var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsync(_options.BaseUrl, content));
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Service call failed with status {StatusCode}: {Response}", response.StatusCode, responseString);
                    return StatusCode((int)response.StatusCode, $"خطا در ارتباط با سرویس شاهکار: {responseString}");
                }

                ShahkarResponse shahkarResponse;
                try
                {
                    shahkarResponse = JsonConvert.DeserializeObject<ShahkarResponse>(responseString);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Invalid JSON response from service: {Response}", responseString);
                    return StatusCode(500, $"خطا: پاسخ سرویس JSON معتبر نیست - {ex.Message}");
                }

                var isMatch = shahkarResponse?.ResponseText?.Contains("\"response\":200") ?? false;

                // Log to ShahkarLog
                var log = new ServicePomixPMO.API.Models.ShahkarLog
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
                    _logger.LogInformation("Logged verification details for NationalId {NationalId} to database", model.NationalId);
                }
                catch (DbUpdateException dbEx)
                {
                    _logger.LogError(dbEx, "Database error while saving log for NationalId {NationalId}", model.NationalId);
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

                try
                {
                    _cache.Set(cacheKey, result, TimeSpan.FromHours(24));
                    _logger.LogInformation("Cached result for {CacheKey}", cacheKey);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache result for {CacheKey}", cacheKey);
                }

                return Ok(result);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed for NationalId {NationalId}", model.NationalId);
                return StatusCode(500, $"خطای ارتباط با سرویس: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during mobile verification for NationalId {NationalId}", model.NationalId);
                return StatusCode(500, $"خطای سرور: {ex.Message}");
            }
        }

        [HttpPost("VerifyDocument")]
        [PermissionAuthorize("CanAccessShahkar")]
        public async Task<IActionResult> VerifyDocument([FromBody] DocumentVerifyViewModel model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for VerifyDocument: {Errors}",
                    string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return BadRequest(ModelState);
            }

            if (string.IsNullOrEmpty(model.DocumentNumber) || !model.DocumentNumber.All(char.IsDigit) || model.DocumentNumber.Length != 18 ||
                string.IsNullOrEmpty(model.VerificationCode) || !model.VerificationCode.All(char.IsDigit) || model.VerificationCode.Length != 6)
            {
                _logger.LogWarning("Invalid DocumentNumber or VerificationCode format: DocumentNumber={DocumentNumber}, VerificationCode={VerificationCode}",
                    model.DocumentNumber, model.VerificationCode);
                return BadRequest("شناسه یکتای سند باید 18 رقم و رمز تصدیق باید 6 رقم باشد.");
            }

            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out long userId))
            {
                _logger.LogWarning("User ID not found or invalid in claims");
                return Unauthorized("کاربر شناسایی نشد.");
            }

            // Check request limit
            if (!await CanMakeShahkarRequest(userId))
            {
                _logger.LogWarning("Request limit exceeded for user {UserId}", userId);
                return BadRequest("محدودیت تعداد درخواست‌ها: لطفاً بعداً تلاش کنید.");
            }

            // Sanitize cache key
            string cacheKey = $"VerifyDoc_{model.DocumentNumber}_{model.VerificationCode}".Replace(" ", "_");
            if (_cache.TryGetValue(cacheKey, out var cachedResult))
            {
                _logger.LogInformation("Cache hit for {CacheKey}", cacheKey);
                return Ok(cachedResult);
            }

            // Generate unique request code
            var requestCode = GenerateRequestId();
            _logger.LogInformation("Generated request ID {RequestId} for document {DocumentNumber}", requestCode, model.DocumentNumber);

            // Prepare request body
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
                _logger.LogInformation("Sending request to {BaseUrl} for document {DocumentNumber}", _options.BaseUrl, model.DocumentNumber);

                // Execute HTTP request with retry policy
                //var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsync(_options.BaseUrl, content));
                var response = await _httpClient.PostAsync(_options.BaseUrl, content);
                var responseString = await response.Content.ReadAsStringAsync();
                var responseDate = DateTime.UtcNow;

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Service call failed with status {StatusCode}: {Response}", response.StatusCode, responseString);
                    return StatusCode((int)response.StatusCode, $"خطا در ارتباط با سرویس: {responseString}");
                }

                JsonDocument jsonDoc;
                try
                {
                    jsonDoc = JsonDocument.Parse(responseString);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Invalid JSON response from service: {Response}", responseString);
                    return StatusCode(500, $"خطا: پاسخ سرویس JSON معتبر نیست - {ex.Message}");
                }

                if (!jsonDoc.RootElement.TryGetProperty("isSuccessful", out var isSuccessful))
                {
                    _logger.LogError("Invalid response structure: Missing 'isSuccessful' property in response: {Response}", responseString);
                    return StatusCode(500, "خطا: ساختار پاسخ سرویس نامعتبر است - ویژگی 'isSuccessful' یافت نشد.");
                }
                if (!jsonDoc.RootElement.TryGetProperty("responseText", out var responseTextElement) || responseTextElement.ValueKind != JsonValueKind.String)
                {
                    _logger.LogError("Invalid response structure: Missing or invalid 'responseText' property in response: {Response}", responseString);
                    return StatusCode(500, "خطا: ساختار پاسخ سرویس نامعتبر است - ویژگی 'responseText' یافت نشد یا معتبر نیست.");
                }

                // Parse the responseText string
                JsonDocument responseTextDoc;
                try
                {
                    responseTextDoc = JsonDocument.Parse(responseTextElement.GetString());
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Invalid JSON in responseText: {ResponseText}", responseTextElement.GetString());
                    return StatusCode(500, $"خطا: محتوای responseText JSON معتبر نیست - {ex.Message}");
                }

                if (!responseTextDoc.RootElement.TryGetProperty("result", out var resultElement))
                {
                    _logger.LogError("Invalid response structure: Missing 'result' property in responseText: {ResponseText}", responseTextElement.GetString());
                    return StatusCode(500, "خطا: ساختار پاسخ سرویس نامعتبر است - ویژگی 'result' در responseText یافت نشد.");
                }

                if (!resultElement.TryGetProperty("data", out var data))
                {
                    _logger.LogError("Invalid response structure: Missing 'data' property in 'result': {ResponseText}", responseTextElement.GetString());
                    return StatusCode(500, "خطا: ساختار پاسخ سرویس نامعتبر است - ویژگی 'data' در result یافت نشد.");
                }

                // Parse UsedQuotaStats
                var quotaStats = new { hourlyUsed = 0, dailyUsed = 0, monthlyUsed = 0 };
                if (jsonDoc.RootElement.TryGetProperty("UsedQuotaStats", out var quotaElement))
                {
                    quotaStats = new
                    {
                        hourlyUsed = quotaElement.TryGetProperty("hourlyUsed", out var h) ? h.GetInt32() : 0,
                        dailyUsed = quotaElement.TryGetProperty("dailyUsed", out var d) ? d.GetInt32() : 0,
                        monthlyUsed = quotaElement.TryGetProperty("monthlyUsed", out var m) ? m.GetInt32() : 0
                    };
                }

                // Parse digital signature
                string digitalSignature = jsonDoc.RootElement.TryGetProperty("digitalSignature", out var sig) ? sig.GetString() ?? "N/A" : "N/A";

                // Helper functions for safe JSON extraction
                string GetString(string key) => data.TryGetProperty(key, out var val) && val.ValueKind != JsonValueKind.Null ? val.GetString() ?? "" : "";
                bool GetBool(string key) => data.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.True;

                // Log to database
                // Log to database - only essential info and full JSON response
                var log = new ServicePomixPMO.API.Models.VerifyDocLog
                {
                    DocumentNumber = model.DocumentNumber ?? "",         // NationalRegisterNo
                    VerificationCode = model.VerificationCode ?? "",     // SecretNo
                    ResponseText = responseString,                       // Full JSON response
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = User.Identity?.Name ?? "Unknown",       // Or use userId if preferred
                };

                // Save log
                _context.VerifyDocLog.Add(log);
                try
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Logged verification details for document {DocumentNumber} to database", model.DocumentNumber);
                }
                catch (DbUpdateException dbEx)
                {
                    _logger.LogError(dbEx, "Database error while saving log for document {DocumentNumber}", model.DocumentNumber);
                    var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                    return StatusCode(500, $"خطای دیتابیس: {innerMessage}");
                }


                // Construct response
                var result = new
                {
                    isSuccessful = isSuccessful.GetBoolean(),
                    error = jsonDoc.RootElement.TryGetProperty("error", out var err) ? err.GetInt32() : 0,
                    errorDescription = jsonDoc.RootElement.TryGetProperty("errorDescription", out var desc) ? desc.GetString() ?? "" : "",
                    UsedQuotaStats = quotaStats,
                    description = jsonDoc.RootElement.TryGetProperty("description", out var desc2) ? desc2.GetString() ?? "" : "",
                    responseStatusCode = (int)response.StatusCode,
                    responseText = responseString,
                    requestDate = jsonDoc.RootElement.TryGetProperty("requestDate", out var reqDate) ? reqDate.GetString() ?? requestDate.ToString("o") : requestDate.ToString("o"),
                    responseDate = jsonDoc.RootElement.TryGetProperty("responseDate", out var resDate) ? resDate.GetString() ?? responseDate.ToString("o") : responseDate.ToString("o"),
                    responseHeaders = new
                    {
                        ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json;charset=UTF-8"
                    },
                    cookies = response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.ToList() : null,
                    digitalSignature,
                    requestId = requestCode
                };

                try
                {
                    _cache.Set(cacheKey, result, TimeSpan.FromHours(24));
                    _logger.LogInformation("Cached result for {CacheKey}", cacheKey);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache result for {CacheKey}", cacheKey);
                }

                _logger.LogInformation("Document verification successful for {DocumentNumber}", model.DocumentNumber);
                return Ok(result);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed for document {DocumentNumber}", model.DocumentNumber);
                return StatusCode(500, $"خطای ارتباط با سرویس: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during document verification for {DocumentNumber}", model.DocumentNumber);
                return StatusCode(500, $"خطای سرور: {ex.Message}");
            }
        }

        private string GenerateRequestId()
        {
            var now = DateTime.Now;
            string dateTimePart = now.ToString("yyyyMMddHHmmss");
            string microseconds = (now.Ticks % 1000000).ToString("D6");
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
        public string BaseUrl { get; set; }
        public Credential Credential { get; set; }
    }

    public class Credential
    {
        public string Code { get; set; }
        public string Password { get; set; }
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