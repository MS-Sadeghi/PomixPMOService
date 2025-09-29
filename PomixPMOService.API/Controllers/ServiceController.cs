using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using PomixPMOService.API.Models.ViewModels;
using ServicePomixPMO;
using ServicePomixPMO.API.Data;
using ServicePomixPMO.API.Models;
using System.Net.Http.Headers;
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

        [HttpPost("ProcessCombinedRequest")]
        [Authorize(Policy = "CanAccessShahkar")]
        public async Task<IActionResult> ProcessCombinedRequest([FromBody] CombinedRequestViewModel model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state: {Errors}", string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return BadRequest(ModelState);
            }

            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (!long.TryParse(userIdClaim, out long userId))
            {
                _logger.LogWarning("Could not extract UserId from JWT token.");
                return Unauthorized("کاربر شناسایی نشد.");
            }

            var requestId = GenerateRequestId();
            var request = new Request
            {
                RequestCode = requestId,
                NationalId = model.NationalId,
                MobileNumber = model.MobileNumber,
                DocumentNumber = model.DocumentNumber,
                VerificationCode = model.VerificationCode,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "Unknown"
            };

            _context.Request.Add(request);
            await _context.SaveChangesAsync();

            long expertId = userId;

            try
            {
                var shahkarTask = CheckMobileNationalCode_Internal(
                    model.NationalId,
                    model.MobileNumber,
                    request.RequestCode,
                    request.RequestId,
                    expertId
                );

                var verifyDocTask = VerifyDocument_Internal(
                    model.DocumentNumber,
                    model.VerificationCode,
                    request.RequestCode,
                    request.RequestId,
                    userId
                );

                await Task.WhenAll(shahkarTask, verifyDocTask);

                var shahkarResult = await shahkarTask;
                var verifyDocResult = await verifyDocTask;

                bool isMatch = false;
                try
                {
                    var internalShahkarResponse = JsonSerializer.Deserialize<InternalShahkarResponse>(
                        shahkarResult.ResponseText,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                    isMatch = internalShahkarResponse?.Result?.Data?.Response == 200;
                    _logger.LogInformation("Shahkar IsMatch for {RequestId}: {IsMatch}", requestId, isMatch);
                }
                catch (System.Text.Json.JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse internal Shahkar response: {ResponseText}", shahkarResult.ResponseText);
                }

                bool isExist = verifyDocResult.ExistDoc;
                bool isNationalIdInResponse = verifyDocResult.PersonsInQuery?.Any(p => p.NationalNo == model.NationalId) ?? false;

                _logger.LogInformation("VerifyDoc results for {RequestId}: IsExist={IsExist}, IsNationalIdInResponse={IsNationalIdInResponse}",
                    requestId, isExist, isNationalIdInResponse);

                request.IsMatch = isMatch;
                request.IsExist = isExist;
                request.IsNationalIdInResponse = isNationalIdInResponse;
                request.UpdatedAt = DateTime.UtcNow;
                request.UpdatedBy = User.Identity?.Name ?? "Unknown";
                _context.Request.Update(request);
                await _context.SaveChangesAsync();

                var combinedResult = new
                {
                    Shahkar = shahkarResult,
                    VerifyDoc = verifyDocResult
                };

                return Ok(combinedResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing combined request for {RequestId}", requestId);
                return StatusCode(500, $"خطا در پردازش درخواست: {ex.Message}");
            }
        }

        private async Task<ShahkarResponse> CheckMobileNationalCode_Internal(
            string nationalId, string mobile, string requestCode, long requestId, long expertId)
        {
            if (nationalId.Length != 10 || !nationalId.All(char.IsDigit))
            {
                _logger.LogWarning("Invalid NationalId format: NationalId={NationalId}", nationalId);
                throw new ArgumentException("کد ملی باید 10 رقم باشد.");
            }
            if (mobile.Length != 11 || !mobile.All(char.IsDigit) || !mobile.StartsWith("09"))
            {
                _logger.LogWarning("Invalid MobileNumber format: MobileNumber={MobileNumber}", mobile);
                throw new ArgumentException("شماره موبایل باید 11 رقم باشد و با 09 شروع شود.");
            }
            if (!IsValidIranianNationalId(nationalId))
            {
                _logger.LogWarning("Invalid NationalId: {NationalId}", nationalId);
                throw new ArgumentException("کد ملی نامعتبر است.");
            }
            if (!await CanMakeShahkarRequest(expertId))
            {
                _logger.LogWarning("Request limit exceeded for expert {ExpertId}", expertId);
                throw new InvalidOperationException("تعداد درخواست‌های شما از حد مجاز گذشته است.");
            }

            string cacheKey = $"Shahkar_{nationalId}_{mobile}";
            if (_cache.TryGetValue(cacheKey, out ShahkarResponse? cachedResult) && cachedResult != null)
            {
                _logger.LogInformation("Returning cached result for {CacheKey}", cacheKey);
                return cachedResult;
            }
             
            try
            {
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
                        new { parameterName = "identificationNo", parameterValue = nationalId },
                        new { parameterName = "requestId", parameterValue = requestCode },
                        new { parameterName = "serviceNumber", parameterValue = mobile }
                    },
                    service = "gsb-itoshahkar"
                };

                var content = new StringContent(JsonSerializer.Serialize(requestContent), Encoding.UTF8, "application/json");
                _logger.LogInformation("Sending request to Shahkar: URL={Url}, RequestContent={RequestContent}, Headers={Headers}",
                    _options.BaseUrl, await content.ReadAsStringAsync(), _httpClient.DefaultRequestHeaders);

                var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsync(_options.BaseUrl, content));
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Received response from Shahkar: StatusCode={StatusCode}, Content={ResponseContent}",
                    response.StatusCode, responseContent);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Shahkar request failed: StatusCode={StatusCode}, Response={ResponseContent}",
                        response.StatusCode, responseContent);
                    throw new HttpRequestException($"خطای سرویس شاهکار: کد {(int)response.StatusCode}");
                }

                ShahkarResponse? result;
                try
                {
                    result = JsonSerializer.Deserialize<ShahkarResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (System.Text.Json.JsonException ex)
                {
                    _logger.LogError(ex, "Failed to deserialize Shahkar response for {RequestId}: ResponseContent={ResponseContent}",
                        requestCode, responseContent);
                    throw new Exception($"خطا در پردازش پاسخ سرویس شاهکار: {ex.Message}");
                }

                if (result == null || string.IsNullOrEmpty(result.ResponseText))
                {
                    _logger.LogWarning("Shahkar response is null or empty for {RequestId}", requestCode);
                    throw new Exception("پاسخ سرویس شاهکار خالی است.");
                }

                bool isMatch = false;
                try
                {
                    var internalResponse = JsonSerializer.Deserialize<InternalShahkarResponse>(result.ResponseText, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (internalResponse?.Result?.Data?.Response == 200)
                    {
                        isMatch = true;
                    }
                    else if (internalResponse?.Result?.Data?.Response == 600)
                    {
                        isMatch = false;
                    }
                    else
                    {
                        _logger.LogWarning("Invalid response code from Shahkar: {ResponseCode}, Comment: {Comment}",
                            internalResponse?.Result?.Data?.Response, internalResponse?.Result?.Data?.Comment);
                        throw new Exception($"خطای نامشخص از سرویس شاهکار: {internalResponse?.Result?.Data?.Comment}");
                    }
                }
                catch (System.Text.Json.JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse internal Shahkar response: {ResponseText}", result.ResponseText);
                    throw new Exception($"خطا در پردازش پاسخ داخلی سرویس شاهکار: {ex.Message}");
                }

                var log = new ShahkarLog
                {
                    NationalId = nationalId,
                    MobileNumber = mobile,
                    RequestCode = requestCode,
                    IsMatch = isMatch,
                    ResponseText = responseContent,
                    ExpertId = expertId,
                    CreatedAt = DateTime.UtcNow,
                    RequestId = requestId
                };
                _context.ShahkarLog.Add(log);
                await _context.SaveChangesAsync();

                result.ResponseStatusCode = (int)response.StatusCode;
                result.RequestId = requestId.ToString();

                try
                {
                    _cache.Set(cacheKey, result, TimeSpan.FromHours(24));
                    _logger.LogInformation("Cached result for {CacheKey}", cacheKey);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache result for {CacheKey}", cacheKey);
                }

                _logger.LogInformation("Mobile verification successful for {NationalId}", nationalId);
                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed for {NationalId}", nationalId);
                throw new Exception($"خطای ارتباط با سرویس شاهکار: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during mobile verification for {NationalId}", nationalId);
                throw new Exception($"خطای سرور: {ex.Message}");
            }
        }

        private async Task<VerifyDocResponse> VerifyDocument_Internal(string documentNumber, string verificationCode, string requestCode, long requestId, long userId)
        {
            if (documentNumber.Length != 18 || !documentNumber.All(char.IsDigit))
                throw new ArgumentException("DocumentNumber نامعتبر است.");
            if (verificationCode.Length != 6 || !verificationCode.All(char.IsDigit))
                throw new ArgumentException("VerificationCode نامعتبر است.");

            var requestBody = new
            {
                credential = new { code = _options.Credential.Code, password = _options.Credential.Password },
                parameters = new object[]
                {
            new { parameterName = "NationalRegisterNo", parameterValue = documentNumber },
            new { parameterName = "SecretNo", parameterValue = verificationCode },
            new { parameterName = "requestId", parameterValue = requestCode }
                },
                service = "gsb-Approval2-GetData"
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            _logger.LogInformation("Sending request to VerifyDoc: URL={Url}, RequestContent={RequestContent}, Headers={Headers}",
                _options.BaseUrl, json, _httpClient.DefaultRequestHeaders);

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsync(_options.BaseUrl, content));
            var responseString = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Received response from VerifyDoc: StatusCode={StatusCode}, Content={ResponseContent}",
                response.StatusCode, responseString);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("VerifyDoc request failed: StatusCode={StatusCode}, Response={ResponseContent}",
                    response.StatusCode, responseString);
                throw new HttpRequestException($"خطای سرویس اصالت سند: کد {(int)response.StatusCode}");
            }

            VerifyDocResponse result = new VerifyDocResponse
            {
                IsSuccessful = false,
                ResponseText = responseString,
                PersonsInQuery = new List<PersonInQuery>(),
                ExistDoc = false
            };

            bool isExist = false;
            bool succseed = false;
            List<PersonInQuery> personsInQuery = new List<PersonInQuery>();

            try
            {
                using var jsonDoc = JsonDocument.Parse(responseString);
                var root = jsonDoc.RootElement;

                // Parse nested responseText
                if (root.TryGetProperty("responseText", out var responseTextElement) && responseTextElement.ValueKind == JsonValueKind.String)
                {
                    try
                    {
                        using var responseTextDoc = JsonDocument.Parse(responseTextElement.GetString());
                        if (responseTextDoc.RootElement.TryGetProperty("result", out var resultElement) &&
                            resultElement.TryGetProperty("data", out var dataElement))
                        {
                            isExist = dataElement.TryGetProperty("ExistDoc", out var existDocElement) && existDocElement.GetBoolean();
                            succseed = dataElement.TryGetProperty("Succseed", out var succseedElement) && succseedElement.GetBoolean();

                            if (dataElement.TryGetProperty("lstFindPersonInQuery", out var personsElement) && personsElement.ValueKind == JsonValueKind.Array)
                            {
                                personsInQuery = JsonSerializer.Deserialize<List<PersonInQuery>>(personsElement.GetRawText(), new JsonSerializerOptions
                                {
                                    PropertyNameCaseInsensitive = true
                                }) ?? new List<PersonInQuery>();
                            }

                            _logger.LogInformation("Parsed VerifyDoc data - isExist: {IsExist}, succseed: {Succseed}, personsInQuery count: {PersonsInQueryCount}",
                                isExist, succseed, personsInQuery.Count);
                        }
                        else
                        {
                            _logger.LogWarning("Missing 'result' or 'data' in responseText for {RequestId}", requestCode);
                        }
                    }
                    catch (JsonException nestedEx)
                    {
                        _logger.LogError(nestedEx, "Failed to parse nested responseText for {RequestId}: {ResponseText}", requestCode, responseTextElement.GetString());
                    }
                }
                else
                {
                    _logger.LogWarning("Missing or invalid 'responseText' property for {RequestId}", requestCode);
                }

                result = new VerifyDocResponse
                {
                    IsSuccessful = isExist || succseed, // Ensure IsSuccessful is true if ExistDoc is true
                    ResponseText = responseString,
                    PersonsInQuery = personsInQuery,
                    ExistDoc = isExist // Explicitly set ExistDoc
                };
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize VerifyDoc response for {RequestId}: ResponseContent={ResponseContent}",
                    requestCode, responseString);
                result = new VerifyDocResponse
                {
                    IsSuccessful = false,
                    ResponseText = responseString,
                    PersonsInQuery = new List<PersonInQuery>(),
                    ExistDoc = false
                };
            }

            var log = new VerifyDocLog
            {
                DocumentNumber = documentNumber,
                VerificationCode = verificationCode,
                ResponseText = responseString,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? userId.ToString(),
                IsExist = isExist, // This will be 1 in DB if ExistDoc is true
                RequestId = requestId
            };

            _logger.LogInformation("Saving VerifyDocLog - DocumentNumber: {DocumentNumber}, IsExist: {IsExist}, RequestId: {RequestId}",
                log.DocumentNumber, log.IsExist, log.RequestId);
            _context.VerifyDocLog.Add(log);
            await _context.SaveChangesAsync();

            return result;
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
            var requestCount = await _context.ShahkarLog
                .CountAsync(r => r.ExpertId == expertId && r.CreatedAt > lastHour);
            _logger.LogInformation("Request count for expert {ExpertId} in last hour: {RequestCount}", expertId, requestCount);
            return requestCount < 10;
        }

        private bool IsValidIranianNationalId(string nationalId)
        {
            if (nationalId.Length != 10 || !nationalId.All(char.IsDigit)) return false;
            int[] digits = nationalId.Select(c => int.Parse(c.ToString())).ToArray();
            int checkDigit = digits[9];
            int sum = Enumerable.Range(0, 9).Sum(i => digits[i] * (10 - i));
            int remainder = sum % 11;
            return remainder < 2 ? checkDigit == remainder : checkDigit == 11 - remainder;
        }

        private string GetJwtToken() => "your-valid-token";
    }

    #region ViewModels & DTOs
    public class CombinedRequestViewModel
    {
        public string NationalId { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public string VerificationCode { get; set; } = string.Empty;
    }
    public class InternalShahkarResponse
    {
        public ResultWrapper? Result { get; set; }
    }

    public class ResultWrapper
    {
        public DataWrapper? Data { get; set; }
        public StatusWrapper? Status { get; set; }
    }
    public class VerifyDocResponse
    {
        public bool IsSuccessful { get; set; }
        public string? ResponseText { get; set; }
        public List<PersonInQuery>? PersonsInQuery { get; set; }
        public bool ExistDoc { get; set; }
    }
    public class DataWrapper
    {
        public string? Result { get; set; }
        public string? RequestId { get; set; }
        public int Response { get; set; }
        public string? Comment { get; set; }
        public string? Id { get; set; }
    }

    public class StatusWrapper
    {
        public int StatusCode { get; set; }
        public string? Message { get; set; }
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
        [JsonPropertyName("isSuccessful")]
        public bool IsSuccessful { get; set; }

        [JsonPropertyName("error")]
        public int Error { get; set; }

        [JsonPropertyName("errorDescription")]
        public string? ErrorDescription { get; set; }

        [JsonPropertyName("responseText")]
        public string? ResponseText { get; set; }

        [JsonPropertyName("responseStatusCode")]
        public int ResponseStatusCode { get; set; }

        [JsonPropertyName("usedQuotaStats")]
        public UsedQuotaStats? UsedQuotaStats { get; set; }

        [JsonPropertyName("requestId")]
        public string? RequestId { get; set; }
    }

    public class UsedQuotaStats
    {
        [JsonPropertyName("hourlyUsed")]
        public int HourlyUsed { get; set; }

        [JsonPropertyName("dailyUsed")]
        public int DailyUsed { get; set; }

        [JsonPropertyName("monthlyUsed")]
        public int MonthlyUsed { get; set; }
    }

    public class VerifyDocInternalResponse
    {
        public VerifyDocResultWrapper? Result { get; set; }
        public StatusWrapper? Status { get; set; }
    }

    public class VerifyDocResultWrapper
    {
        public JsonElement Data { get; set; }
        public StatusWrapper? Status { get; set; }
    }

    public class VerifyDocDataWrapper
    {
        public List<object>? RegCases { get; set; }
        public List<object>? BaseDocuments { get; set; }
        public List<object>? FollowerDocuments { get; set; }
        public bool Succseed { get; set; }
        public string? NationalRegisterNo { get; set; }
        public string? DocType { get; set; }
        public string? DocType_code { get; set; }
        public bool HasPermission { get; set; }
        public bool ExistDoc { get; set; }
        public string? Desc { get; set; }
        public string? ScriptoriumName { get; set; }
        public string? SignGetterTitle { get; set; }
        public string? SignSubject { get; set; }
        public string? DocDate { get; set; }
        public string? CaseClasifyNo { get; set; }
        public string? ImpotrtantAnnexText { get; set; }
        public string? DocImage { get; set; }
        public string? DocImage_Base64 { get; set; }
        public string? ADVOCACYENDDATE { get; set; }
        public List<PersonInQuery>? LstFindPersonInQuery { get; set; }
    }

    public class PersonInQuery
    {
        public string? NationalNo { get; set; }
        public string? Birthdate { get; set; }
        public string? Name { get; set; }
        public string? Family { get; set; }
        public string? AgentType { get; set; }
        public string? PersonType { get; set; }
        public string? PersonType_code { get; set; }
        public string? NationalNoMovakel { get; set; }
        public string? NameMovakel { get; set; }
        public string? FamilyMovakel { get; set; }
        public string? TxtRelation { get; set; }
        public string? RoleType { get; set; }
        public string? Person_RoleType_code { get; set; }
    }
    #endregion
}