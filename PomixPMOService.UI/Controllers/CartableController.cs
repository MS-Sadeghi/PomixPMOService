using DNTCaptcha.Core;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;
using PomixPMOService.UI.Controllers;
using System.Dynamic;
using PomixPMOService.API.Controllers;
using static ServicePomixPMO.API.Controllers.RequestController;

namespace PomixPMOService.UI.Controllers
{
    public class CartableController : Controller
    {
        private readonly HttpClient _client;
        private readonly IDNTCaptchaValidatorService _captchaValidatorService;
        private readonly ILogger<CartableController> _logger;

        public CartableController(
            IHttpClientFactory httpClientFactory,
            IDNTCaptchaValidatorService captchaValidatorService,
            ILogger<CartableController> logger)
        {
            _client = httpClientFactory.CreateClient("PomixApi");
            _captchaValidatorService = captchaValidatorService ?? throw new ArgumentNullException(nameof(captchaValidatorService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, string search = "")
        {
            var model = await GetCartableData(page, search);
            // دریافت وضعیت IsRead از API برای هر آیتم
            foreach (var item in model.Items)
            {
                try
                {
                    var token = HttpContext.Session.GetString("JwtToken") ?? ViewBag.JwtToken;
                    if (!string.IsNullOrEmpty(token))
                    {
                        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    }

                    var response = await _client.GetAsync($"Service/GetTextByRequestId/{item.RequestId}");
                    if (response.IsSuccessStatusCode)
                    {
                        var apiResponse = await response.Content.ReadFromJsonAsync<DocTextResponse>();
                        item.IsRead = apiResponse?.IsRead ?? false;
                    }
                    else
                    {
                        item.IsRead = false;
                        _logger.LogWarning("Failed to fetch IsRead for RequestId: {RequestId}", item.RequestId);
                    }
                }
                catch (Exception ex)
                {
                    item.IsRead = false;
                    _logger.LogError(ex, "Error fetching IsRead for RequestId: {RequestId}", item.RequestId);
                }
            }
            ViewBag.FormModel = new CartableFormViewModel();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ValidateSteps(CartableFormViewModel model)
        {
            bool isValid = true;
            model.Step1Message = model.Step2Message = model.Step3Message = "";

            // گام 1: احراز هویت با سرویس شاهکار
            if (string.IsNullOrEmpty(model.NationalCode) || model.NationalCode.Length != 10 || !System.Text.RegularExpressions.Regex.IsMatch(model.NationalCode, @"^\d{10}$"))
            {
                model.Step1Message = "کد ملی نامعتبر است.";
                isValid = false;
            }
            if (string.IsNullOrEmpty(model.MobileNumber) || model.MobileNumber.Length != 11 || !System.Text.RegularExpressions.Regex.IsMatch(model.MobileNumber, @"^09\d{9}$"))
            {
                model.Step1Message += " شماره موبایل نامعتبر است.";
                isValid = false;
            }

            if (isValid)
            {
                try
                {
                    var token = HttpContext.Session.GetString("JwtToken") ?? ViewBag.JwtToken;
                    if (string.IsNullOrEmpty(token))
                    {
                        ViewBag.ErrorMessage = "لطفاً ابتدا وارد سیستم شوید.";
                        return RedirectToAction("LoginPage", "Home");
                    }
                    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    var response = await _client.PostAsJsonAsync("Service/ProcessCombinedRequest", new CombinedRequestViewModel
                    {
                        NationalId = model.NationalCode,
                        MobileNumber = model.MobileNumber,
                        DocumentNumber = model.DocumentNumber,
                        VerificationCode = model.VerifyCode
                    });

                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadFromJsonAsync<dynamic>();
                        bool isMatch = result?.data?.Shahkar?.IsSuccessful ?? false;
                        if (isMatch)
                        {
                            model.Step1Message = "احراز هویت معتبر است.";
                        }
                        else
                        {
                            model.Step1Message = "کد ملی و شماره موبایل تطابق ندارند.";
                            isValid = false;
                        }
                    }
                    else
                    {
                        model.Step1Message = "خطا در ارتباط با سرویس احراز هویت.";
                        isValid = false;
                    }
                }
                catch (Exception ex)
                {
                    model.Step1Message = $"خطا در ارتباط با سرویس احراز هویت: {ex.Message}";
                    isValid = false;
                }
            }

            // گام 2: احراز سند
            if (string.IsNullOrEmpty(model.VerifyCode) || model.VerifyCode.Length != 6 || !System.Text.RegularExpressions.Regex.IsMatch(model.VerifyCode, @"^\d{6}$"))
            {
                model.Step2Message = "رمز تصدیق نامعتبر است.";
                isValid = false;
            }
            if (string.IsNullOrEmpty(model.DocumentNumber) || model.DocumentNumber.Length != 18 || !System.Text.RegularExpressions.Regex.IsMatch(model.DocumentNumber, @"^\d{18}$"))
            {
                model.Step2Message += " شناسه سند نامعتبر است.";
                isValid = false;
            }
            if (isValid && string.IsNullOrEmpty(model.Step2Message))
            {
                model.Step2Message = "احراز سند معتبر است.";
            }

            // گام 3: تطابق کد ملی
            if (string.IsNullOrEmpty(model.ClientNationalCode) || model.ClientNationalCode.Length != 10 || !System.Text.RegularExpressions.Regex.IsMatch(model.ClientNationalCode, @"^\d{10}$"))
            {
                model.Step3Message = "کد ملی موکل نامعتبر است.";
                isValid = false;
            }
            else
            {
                model.Step3Message = "تطابق کد ملی معتبر است.";
            }

            if (isValid)
            {
                ViewBag.SuccessMessage = "همه گام‌ها معتبر هستند.";
            }

            ViewBag.FormModel = model;
            return View("Index", await GetCartableData(1, ""));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitRequest(CartableFormViewModel model)
        {
            if (!_captchaValidatorService.HasRequestValidCaptchaEntry())
            {
                ViewBag.ErrorMessage = "کد امنیتی اشتباه است.";
                ViewBag.FormModel = model;
                return View("Index", await GetCartableData(1, ""));
            }

            if (!model.AgreeToTerms)
            {
                ViewBag.ErrorMessage = "لطفاً با شرایط موافقت کنید.";
                ViewBag.FormModel = model;
                return View("Index", await GetCartableData(1, ""));
            }

            try
            {
                var token = HttpContext.Session.GetString("JwtToken") ?? ViewBag.JwtToken;
                if (string.IsNullOrEmpty(token))
                {
                    ViewBag.ErrorMessage = "لطفاً ابتدا وارد سیستم شوید.";
                    return RedirectToAction("LoginPage", "Home");
                }
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var requestData = new
                {
                    NationalId = model.ClientNationalCode,
                    MobileNumber = model.MobileNumber,
                    DocumentNumber = model.DocumentNumber,
                    VerificationCode = model.VerifyCode
                };

                var response = await _client.PostAsJsonAsync("Service/ProcessCombinedRequest", requestData);
                if (response.IsSuccessStatusCode)
                {
                    var combinedResult = await response.Content.ReadFromJsonAsync<dynamic>();
                    var verifyDoc = combinedResult.data.VerifyDoc;
                    var verifyDocResponseText = (string)verifyDoc.ResponseText;

                    dynamic verifyDocData = new ExpandoObject();
                    ViewBag.DocumentError = null;

                    try
                    {
                        var responseTextDoc = JsonDocument.Parse(verifyDocResponseText);
                        var dataElement = responseTextDoc.RootElement.GetProperty("result").GetProperty("data");

                        verifyDocData.DocType = dataElement.TryGetProperty("DocType", out var docType) ? docType.GetString() : null;
                        verifyDocData.SignSubject = dataElement.TryGetProperty("SignSubject", out var signSubject) ? signSubject.GetString() : null;
                        verifyDocData.DocDate = dataElement.TryGetProperty("DocDate", out var docDate) ? docDate.GetString() : null;
                        verifyDocData.Desc = dataElement.TryGetProperty("Desc", out var desc) ? desc.GetString() : null;
                        verifyDocData.DocImage = dataElement.TryGetProperty("DocImage", out var docImage) ? docImage.GetString() : null;
                        verifyDocData.DocImage_Base64 = dataElement.TryGetProperty("DocImage_Base64", out var docImageBase64) ? docImageBase64.GetString() : null;
                        verifyDocData.ImpotrtantAnnexText = dataElement.TryGetProperty("ImpotrtantAnnexText", out var importantAnnexText) ? importantAnnexText.GetString() : null;

                        var lstPersons = dataElement.TryGetProperty("LstFindPersonInQuery", out var personsElement) && personsElement.ValueKind == JsonValueKind.Array
                            ? JsonSerializer.Deserialize<List<dynamic>>(personsElement.GetRawText())
                            : new List<dynamic>();

                        verifyDocData.LstFindPersonInQuery = lstPersons;

                        ViewBag.RequestId = combinedResult.data.RequestId != null ? combinedResult.data.RequestId : 0;
                    }
                    catch (Exception)
                    {
                        ViewBag.DocumentError = "خطا در پردازش محتوای سند.";
                    }

                    ViewBag.VerifyDocData = verifyDocData;
                    ViewBag.ShowDocumentTab = true;
                    ViewBag.SuccessMessage = "درخواست با موفقیت ثبت شد و سند آماده نمایش است.";
                    ViewBag.FormModel = new CartableFormViewModel();
                    return View("Index", await GetCartableData(1, ""));
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    ViewBag.ErrorMessage = $"خطا در ثبت درخواست: {error}";
                    ViewBag.FormModel = model;
                    return View("Index", await GetCartableData(1, ""));
                }
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"خطا در ارتباط با سرور: {ex.Message}";
                ViewBag.FormModel = model;
                return View("Index", await GetCartableData(1, ""));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateValidationStatus(long requestId, bool validateByExpert)
        {
            try
            {
                _logger.LogInformation("Received UpdateValidationStatus request for RequestId: {RequestId}, ValidateByExpert: {ValidateByExpert}", requestId, validateByExpert);

                var token = HttpContext.Session.GetString("JwtToken") ?? ViewBag.JwtToken;
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("JWT token not found for RequestId: {RequestId}", requestId);
                    return Json(new { success = false, message = "توکن یافت نشد. لطفاً دوباره وارد سیستم شوید." });
                }

                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                _logger.LogInformation("Sending POST to Request/UpdateValidationStatus/{RequestId}/{ValidateByExpert}", requestId, validateByExpert);
                var response = await _client.PostAsync($"Request/UpdateValidationStatus/{requestId}/{validateByExpert}", null);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("API call successful for RequestId: {RequestId}, Response: {Response}", requestId, responseContent);
                    return Json(new
                    {
                        success = true,
                        message = validateByExpert ? "درخواست با موفقیت تأیید شد ✅" : "درخواست با موفقیت رد شد ❌"
                    });
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("API call failed for RequestId: {RequestId}, StatusCode: {StatusCode}, Error: {Error}", requestId, response.StatusCode, error);
                    return Json(new
                    {
                        success = false,
                        message = string.IsNullOrEmpty(error) ? "خطا در به‌روزرسانی وضعیت درخواست: پاسخ سرور خالی است." : $"خطا در به‌روزرسانی وضعیت درخواست: {error}"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating validation status for RequestId: {RequestId}", requestId);
                return Json(new { success = false, message = $"خطا در ارتباط با سرور: {ex.Message}" });
            }
        }
    



        [HttpPost]
        public async Task<IActionResult> MarkDocumentAsRead(long requestId)
        {
            try
            {
                var token = HttpContext.Session.GetString("JwtToken") ?? ViewBag.JwtToken;
                if (string.IsNullOrEmpty(token))
                {
                    return Json(new { success = false, message = "توکن یافت نشد. لطفاً دوباره وارد سیستم شوید." });
                }

                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _client.PostAsync($"Service/MarkDocumentAsRead/{requestId}", null);

                if (response.IsSuccessStatusCode)
                {
                    return Json(new { success = true, message = "سند با موفقیت به عنوان خوانده شده علامت‌گذاری شد." });
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return Json(new { success = false, message = $"خطا در علامت‌گذاری سند: {error}" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"خطا در ارتباط با سرور: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDocumentText(long requestId)
        {
            try
            {
                var token = HttpContext.Session.GetString("JwtToken") ?? ViewBag.JwtToken;
                if (string.IsNullOrEmpty(token))
                {
                    return Json(new { success = false, message = "توکن یافت نشد. لطفاً دوباره وارد سیستم شوید." });
                }

                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _client.GetAsync($"Service/GetTextByRequestId/{requestId}");

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = await response.Content.ReadAsStringAsync();
                    return Content(apiResponse, "application/json");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return Json(new { success = false, message = $"خطا در دریافت متن سند: {error}" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"خطا در دریافت متن سند: {ex.Message}" });
            }
        }

        private async Task<PaginatedCartableViewModel> GetCartableData(int page, string search)
        {
            var pageSize = 10;
            var url = $"Request?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search))
            {
                url += $"&search={System.Web.HttpUtility.UrlEncode(search)}";
            }

            try
            {
                var token = HttpContext.Session.GetString("JwtToken") ?? ViewBag.JwtToken;
                if (!string.IsNullOrEmpty(token))
                {
                    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                var response = await _client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<PaginatedResponse<CartableItemViewModel>>();
                    return new PaginatedCartableViewModel
                    {
                        Items = data?.Items ?? new List<CartableItemViewModel>(),
                        TotalCount = data?.TotalCount ?? 0,
                        CurrentPage = page,
                        PageSize = pageSize,
                        SearchQuery = search
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching cartable data for page {Page} with search {Search}", page, search);
            }
            return new PaginatedCartableViewModel { CurrentPage = page, PageSize = pageSize, SearchQuery = search };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ValidateShahkar(CartableFormViewModel model)
        {
            try
            {
                // اعتبارسنجی ورودی‌ها
                if (string.IsNullOrEmpty(model.NationalCode) || model.NationalCode.Length != 10 || !System.Text.RegularExpressions.Regex.IsMatch(model.NationalCode, @"^\d{10}$"))
                {
                    return Json(new { success = false, message = "کد ملی نامعتبر است." });
                }
                if (string.IsNullOrEmpty(model.MobileNumber) || model.MobileNumber.Length != 11 || !System.Text.RegularExpressions.Regex.IsMatch(model.MobileNumber, @"^09\d{9}$"))
                {
                    return Json(new { success = false, message = "شماره موبایل نامعتبر است." });
                }

                // دریافت توکن JWT
                var token = HttpContext.Session.GetString("JwtToken") ?? ViewBag.JwtToken;
                if (string.IsNullOrEmpty(token))
                {
                    return Json(new { success = false, message = "لطفاً ابتدا وارد سیستم شوید." });
                }
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // ارسال درخواست به سرویس شاهکار
                var response = await _client.PostAsJsonAsync("Service/CheckMobileNationalCode", new
                {
                    NationalId = model.NationalCode,
                    MobileNumber = model.MobileNumber
                });

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<ShahkarResponse>();
                    bool isMatch = false;
                    try
                    {
                        var internalResponse = JsonSerializer.Deserialize<InternalShahkarResponse>(result.ResponseText, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        isMatch = internalResponse?.Result?.Data?.Response == 200;
                    }
                    catch (System.Text.Json.JsonException)
                    {
                        return Json(new { success = false, message = "خطا در پردازش پاسخ سرویس شاهکار." });
                    }

                    if (isMatch)
                    {
                        return Json(new { success = true, message = "احراز هویت با موفقیت انجام شد." });
                    }
                    else
                    {
                        return Json(new { success = false, message = "کد ملی و شماره موبایل تطابق ندارند." });
                    }
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return Json(new { success = false, message = $"خطا در ارتباط با سرویس شاهکار: {error}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در احراز هویت با سرویس شاهکار برای کد ملی {NationalCode}", model.NationalCode);
                return Json(new { success = false, message = $"خطا در سرور: {ex.Message}" });
            }
        }

        [HttpGet]
        public IActionResult Shahkar()
        {
            return View();
        }

        [HttpGet]
        public IActionResult VerifyDocument()
        {
            return View();
        }

    }

    public static class PersianDateHelper
    {
        public static string ToPersianDate(this DateTime date)
        {
            try
            {
                var persianCalendar = new System.Globalization.PersianCalendar();
                return $"{persianCalendar.GetYear(date)}/{persianCalendar.GetMonth(date):D2}/{persianCalendar.GetDayOfMonth(date):D2}";
            }
            catch
            {
                return "نامعتبر";
            }
        }

        public static string ToPersianTime(this DateTime date)
        {
            try
            {
                return $"{date:HH:mm:ss}";
            }
            catch
            {
                return "نامعتبر";
            }
        }
    }

    public class PaginatedResponse<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    public class PaginatedCartableViewModel
    {
        public List<CartableItemViewModel> Items { get; set; } = new List<CartableItemViewModel>();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public string SearchQuery { get; set; } = string.Empty;
    }

    public class CartableFormViewModel
    {
        public string NationalCode { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public string VerifyCode { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public string ClientNationalCode { get; set; } = string.Empty;
        public bool AgreeToTerms { get; set; }
        public string Step1Message { get; set; } = string.Empty;
        public string Step2Message { get; set; } = string.Empty;
        public string Step3Message { get; set; } = string.Empty;
    }

    public class CartableItemViewModel
    {
        public long RequestId { get; set; }
        public string RequestCode { get; set; } = string.Empty;
        public string NationalId { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public string VerificationCode { get; set; } = string.Empty;
        public string ImpotrtantAnnexText { get; set; } = string.Empty;
        public bool? IsMatch { get; set; }
        public bool? IsExist { get; set; }
        public bool? IsNationalIdInResponse { get; set; }
        public bool? IsNationalIdInLawyers { get; set; }
        public bool? ValidateByExpert { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public bool IsRead { get; set; } // اضافه شده برای وضعیت خوانده شدن سند
    }

    public class CombinedRequestViewModel
    {
        public string NationalId { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public string VerificationCode { get; set; } = string.Empty;
    }

    public class DocTextResponse
    {
        public bool Success { get; set; }
        public string? DocumentText { get; set; }
        public bool IsRead { get; set; }
        public string? Message { get; set; }
    }
}