using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using ServicePomixPMO;
using ServicePomixPMO.API.Data;
using ServicePomixPMO.API.Models; 
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PomixPMOService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ServiceController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly PomixServiceContext _context;
        private readonly ShahkarServiceOptions _options;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ServiceController> _logger;
        private readonly string _providerCode = "0785";
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

        public ServiceController(
            HttpClient httpClient,
            PomixServiceContext context,
            IOptions<ShahkarServiceOptions> options,
            IMemoryCache cache,
            ILogger<ServiceController> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (string.IsNullOrEmpty(_options.BaseUrl) || _options.Credential == null ||
                string.IsNullOrEmpty(_options.Credential.Code) || string.IsNullOrEmpty(_options.Credential.Password))
            {
                _logger.LogError("Shahkar service configuration is incomplete: BaseUrl={BaseUrl}, Code={Code}, Password={Password}",
                    _options.BaseUrl, _options.Credential?.Code, _options.Credential?.Password);
                throw new InvalidOperationException("تنظیمات سرویس شاهکار ناقص است.");
            }

            _retryPolicy = Policy
                .Handle<HttpRequestException>()
                .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (result, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning("Retry {RetryCount} after {TimeSpan} seconds due to {Reason}",
                            retryCount, timeSpan.TotalSeconds, result.Exception?.Message ?? $"StatusCode: {result.Result?.StatusCode}");
                    });
        }

        [HttpPost("CheckMobileNationalCode")]
        [Authorize(Policy = "CanAccessShahkar")]
        public async Task<IActionResult> CheckMobileNationalCode([FromBody] MobileCheckViewModel model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for CheckMobileNationalCode: {Errors}",
                    string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return BadRequest(ModelState);
            }

            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (!long.TryParse(userIdClaim, out long expertId))
            {
                _logger.LogWarning("Could not extract UserId from JWT token.");
                return Unauthorized("کاربر شناسایی نشد.");
            }

            if (!await CanMakeShahkarRequest(expertId))
            {
                _logger.LogWarning("Request limit exceeded for expert {ExpertId}", expertId);
                return StatusCode(429, "تعداد درخواست‌های شما از حد مجاز گذشته است.");
            }

            // اعتبارسنجی دقیق‌تر
            if (model.NationalId.Length != 10 || !model.NationalId.All(char.IsDigit))
            {
                _logger.LogWarning("Invalid NationalId format: NationalId={NationalId}", model.NationalId);
                return BadRequest("کد ملی باید 10 رقم باشد.");
            }

            if (model.MobileNumber.Length != 11 || !model.MobileNumber.All(char.IsDigit) || !model.MobileNumber.StartsWith("09"))
            {
                _logger.LogWarning("Invalid MobileNumber format: MobileNumber={MobileNumber}", model.MobileNumber);
                return BadRequest("شماره موبایل باید 11 رقم باشد و با 09 شروع شود.");
            }

            if (!IsValidIranianNationalId(model.NationalId))
            {
                _logger.LogWarning("Invalid NationalId: {NationalId}", model.NationalId);
                return BadRequest("کد ملی نامعتبر است.");
            }

            string cacheKey = $"Shahkar_{model.NationalId}_{model.MobileNumber}";
            if (_cache.TryGetValue(cacheKey, out ShahkarResponse? cachedResult) && cachedResult != null)
            {
                _logger.LogInformation("Returning cached result for {CacheKey}", cacheKey);
                return Ok(cachedResult);
            }

            try
            {
                string requestCode = GenerateRequestId();
                var requestContent = new
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

                var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(requestContent), Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization = null;

                _logger.LogInformation("Sending request to Shahkar: URL={Url}, RequestContent={RequestContent}",
                    $"{_options.BaseUrl}", System.Text.Json.JsonSerializer.Serialize(requestContent));

                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsync($"{_options.BaseUrl}", content));

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Received response from Shahkar: StatusCode={StatusCode}, Content={ResponseContent}",
                    response.StatusCode, responseContent);

                ShahkarResponse? result = null;
                try
                {
                    result = System.Text.Json.JsonSerializer.Deserialize<ShahkarResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (System.Text.Json.JsonException ex)
                {
                    _logger.LogError(ex, "Failed to deserialize Shahkar response for {RequestId}: ResponseContent={ResponseContent}",
                        requestCode, responseContent);
                    return StatusCode(500, $"خطا در پردازش پاسخ سرویس شاهکار: {ex.Message}");
                }

                if (result == null)
                {
                    _logger.LogWarning("Shahkar response is null for {RequestId}", requestCode);
                    return StatusCode(500, "پاسخ سرویس شاهکار خالی است.");
                }

                // پارس پاسخ داخلی برای بررسی نتیجه واقعی
                bool isMatch = false;
                try
                {
                    var internalResponse = System.Text.Json.JsonSerializer.Deserialize<InternalShahkarResponse>(result.ResponseText, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (internalResponse?.Result?.Data?.Response == 200)
                    {
                        isMatch = true; // تطابق موفق
                    }
                    else if (internalResponse?.Result?.Data?.Response == 600)
                    {
                        isMatch = false; // عدم تطابق
                    }
                    else
                    {
                        _logger.LogWarning("Invalid response code from Shahkar: {ResponseCode}, Comment: {Comment}",
                            internalResponse?.Result?.Data?.Response, internalResponse?.Result?.Data?.Comment);
                        return BadRequest(new
                        {
                            Error = internalResponse?.Result?.Data?.Response ?? 0,
                            ErrorDescription = internalResponse?.Result?.Data?.Comment ?? "خطای نامشخص از سرویس شاهکار"
                        });
                    }
                }
                catch (System.Text.Json.JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse internal Shahkar response: {ResponseText}", result.ResponseText);
                    return StatusCode(500, $"خطا در پردازش پاسخ داخلی سرویس شاهکار: {ex.Message}");
                }

                var log = new ShahkarLog
                {
                    NationalId = model.NationalId,
                    MobileNumber = model.MobileNumber,
                    RequestCode = requestCode,
                    IsMatch = isMatch,
                    ResponseText = responseContent,
                    ExpertId = expertId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ShahkarLog.Add(log);
                await _context.SaveChangesAsync();

                if (!response.IsSuccessStatusCode || !result.IsSuccessful)
                {
                    _logger.LogWarning("Shahkar request failed for {RequestId}: Error={Error}, ErrorDescription={ErrorDescription}",
                        requestCode, result.Error, result.ErrorDescription);
                    return BadRequest(new { result.Error, ErrorDescription = result.ErrorDescription ?? "خطای نامشخص از سرویس شاهکار" });
                }

                result.ResponseStatusCode = (int)response.StatusCode;
                result.RequestDate = DateTime.UtcNow.AddHours(-1).ToString("yyyy-MM-dd HH:mm:ss");
                result.ResponseDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                result.ResponseHeaders = new ResponseHeaders
                {
                    ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json;charset=UTF-8"
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

                _logger.LogInformation("Mobile verification successful for {NationalId}", model.NationalId);
                return Ok(result);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed for {NationalId}", model.NationalId);
                return StatusCode(500, $"خطای ارتباط با سرویس شاهکار: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during mobile verification for {NationalId}", model.NationalId);
                return StatusCode(500, $"خطای سرور: {ex.Message}");
            }
        }

        [HttpPost("VerifyDocument")]
        [Authorize(Policy = "CanAccessShahkar")]
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
                catch (System.Text.Json.JsonException ex)
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
                catch (System.Text.Json.JsonException ex)
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
            string dateTimePart = now.ToString("yyyyMMddHHmmss"); // 14 رقم
            string microseconds = (now.Ticks % 1000000).ToString("D6"); // دقیقاً 6 رقم
            string requestId = $"{_providerCode}{dateTimePart}{microseconds}"; // 4 + 14 + 6 = 24 رقم
            _logger.LogInformation("Generated RequestId: {RequestId}, Length: {Length}", requestId, requestId.Length);
            return requestId;
        }

        private async Task<bool> CanMakeShahkarRequest(long expertId)
        {
            var lastHour = DateTime.UtcNow.AddHours(-1);
            var requestCount = await _context.Request
                .CountAsync(r => r.ExpertId == expertId && r.CreatedAt > lastHour);
            _logger.LogInformation("Request count for expert {ExpertId} in last hour: {RequestCount}", expertId, requestCount);
            return requestCount < 10;
        }

        private bool IsValidIranianNationalId(string nationalId)
        {
            if (nationalId.Length != 10 || !nationalId.All(char.IsDigit))
                return false;

            int[] digits = nationalId.Select(c => int.Parse(c.ToString())).ToArray();
            int checkDigit = digits[9];
            int sum = 0;
            for (int i = 0; i < 9; i++)
                sum += digits[i] * (10 - i);
            int remainder = sum % 11;
            return remainder < 2 ? checkDigit == remainder : checkDigit == 11 - remainder;
        }
    }

    public class InternalShahkarResponse
    {
        public InternalResult? Result { get; set; }
        public InternalStatus? Status { get; set; }
    }


    public class InternalVerifyDocResponse
    {
        public InternalResult? Result { get; set; }
        public InternalStatus? Status { get; set; }
    }

    public class InternalResult
    {
        public InternalData? Data { get; set; }
        public InternalStatus? Status { get; set; }
    }

    public class InternalData
    {
        public string? Result { get; set; }
        public string? RequestId { get; set; }
        public int Response { get; set; }
        public string? Comment { get; set; }
    }

    public class InternalStatus
    {
        public int StatusCode { get; set; }
        public string? Message { get; set; }
    }

    public class MobileCheckViewModel
    {
        [System.Text.Json.Serialization.JsonPropertyName("nationalId")]
        public string NationalId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("mobileNumber")]
        public string MobileNumber { get; set; } = string.Empty;
    }

    public class DocumentVerifyViewModel
    {
        [System.Text.Json.Serialization.JsonPropertyName("documentNumber")]
        public string DocumentNumber { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("verificationCode")]
        public string VerificationCode { get; set; } = string.Empty;
    }

    public class ShahkarServiceOptions
    {
        public string BaseUrl { get; set; } = string.Empty;
        public Credential Credential { get; set; } = new Credential();
    }

    public class Credential
    {
        public string Code { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class ShahkarResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("isSuccessful")]
        public bool IsSuccessful { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("error")]
        public int Error { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("errorDescription")]
        public string? ErrorDescription { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("responseText")]
        public string? ResponseText { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("responseStatusCode")]
        public int ResponseStatusCode { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("usedQuotaStats")]
        public UsedQuotaStats? UsedQuotaStats { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("description")]
        public string? Description { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("requestDate")]
        public string? RequestDate { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("responseDate")]
        public string? ResponseDate { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("responseHeaders")]
        public ResponseHeaders? ResponseHeaders { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("digitalSignature")]
        public string? DigitalSignature { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("requestId")]
        public string? RequestId { get; set; }
    }

    public class UsedQuotaStats
    {
        [System.Text.Json.Serialization.JsonPropertyName("hourlyUsed")]
        public int HourlyUsed { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("dailyUsed")]
        public int DailyUsed { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("monthlyUsed")]
        public int MonthlyUsed { get; set; }
    }

    public class ResponseHeaders
    {
        [System.Text.Json.Serialization.JsonPropertyName("contentType")]
        public string? ContentType { get; set; }
    }
}