using DNTCaptcha.Core;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PomixPMOService.API.Controllers;
using PomixPMOService.UI.Filters;
using System.Dynamic;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace PomixPMOService.UI.Controllers
{
    [NoCacheFilter]
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

        #region CartableIndex

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, string search = "", string filterStatus = "")
        {
            // گرفتن داده‌ها از API
            var model = await GetCartableData(page, search);

            // اعمال فیلتر بر اساس وضعیت
            if (!string.IsNullOrEmpty(filterStatus))
            {
                switch (filterStatus.ToLower())
                {
                    case "approved":
                        model.Items = model.Items.Where(x => x.ValidateByExpert == true).ToList();
                        break;
                    case "rejected":
                        model.Items = model.Items.Where(x => x.ValidateByExpert == false).ToList();
                        break;
                    case "pending":
                        model.Items = model.Items.Where(x => x.ValidateByExpert == null).ToList();
                        break;
                }
            }

            // بررسی خوانده شدن سندها
            foreach (var item in model.Items)
            {
                try
                {
                    var token = HttpContext.Session.GetString("JwtToken") ?? ViewBag.JwtToken;
                    if (!string.IsNullOrEmpty(token))
                        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    var response = await _client.GetAsync($"Service/GetTextByRequestId/{item.RequestId}");
                    if (response.IsSuccessStatusCode)
                    {
                        var apiResponse = await response.Content.ReadFromJsonAsync<DocTextResponse>();
                        item.IsRead = apiResponse?.IsRead ?? false;
                    }
                    else
                    {
                        item.IsRead = false;
                    }
                }
                catch (Exception)
                {
                    item.IsRead = false;
                }
            }

            ViewBag.FormModel = new CartableFormViewModel();
            ViewBag.FilterStatus = filterStatus;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ValidateSteps(CartableFormViewModel model)
        {
            // ریست کردن پیام‌ها
            model.Step1Message = model.Step2Message = model.Step3Message = "";

            bool step1Valid = true, step2Valid = true, step3Valid = true;

            // 🟢 گام 1: احراز هویت با سرویس شاهکار
            if (string.IsNullOrEmpty(model.NationalCode) || model.NationalCode.Length != 10 || !Regex.IsMatch(model.NationalCode, @"^\d{10}$"))
            {
                model.Step1Message = "کد ملی نامعتبر است.";
                step1Valid = false;
            }
            if (string.IsNullOrEmpty(model.MobileNumber) || model.MobileNumber.Length != 11 || !Regex.IsMatch(model.MobileNumber, @"^09\d{9}$"))
            {
                model.Step1Message += " شماره موبایل نامعتبر است.";
                step1Valid = false;
            }

            if (step1Valid)
            {
                try
                {
                    var token = HttpContext.Session.GetString("JwtToken") ?? ViewBag.JwtToken;
                    if (string.IsNullOrEmpty(token))
                    {
                        model.Step1Message = "لطفاً ابتدا وارد سیستم شوید.";
                        step1Valid = false;
                    }
                    else
                    {
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
                            var content = await response.Content.ReadAsStringAsync();
                            dynamic result = JsonConvert.DeserializeObject<dynamic>(content);
                            bool isMatch = result?.data?.Shahkar?.IsSuccessful == true;

                            model.Step1Message = isMatch ? "احراز هویت معتبر است." : "کد ملی و شماره موبایل تطابق ندارند.";
                            step1Valid = isMatch;
                        }
                        else
                        {
                            model.Step1Message = "خطا در ارتباط با سرویس احراز هویت.";
                            step1Valid = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    model.Step1Message = $"خطا در ارتباط با سرویس احراز هویت: {ex.Message}";
                    step1Valid = false;
                }
            }

            // 🟢 گام 2: احراز سند (مستقل از گام 1)
            if (string.IsNullOrEmpty(model.VerifyCode) || model.VerifyCode.Length != 6 || !Regex.IsMatch(model.VerifyCode, @"^\d{6}$"))
            {
                model.Step2Message = "رمز تصدیق نامعتبر است.";
                step2Valid = false;
            }
            if (string.IsNullOrEmpty(model.DocumentNumber) || model.DocumentNumber.Length != 18 || !Regex.IsMatch(model.DocumentNumber, @"^\d{18}$"))
            {
                model.Step2Message += " شناسه سند نامعتبر است.";
                step2Valid = false;
            }
            if (step2Valid && string.IsNullOrEmpty(model.Step2Message))
            {
                model.Step2Message = "احراز سند معتبر است.";
            }

            // 🟢 گام 3: تطابق کد ملی موکل (مستقل از گام‌های دیگر)
            if (string.IsNullOrEmpty(model.ClientNationalCode) || model.ClientNationalCode.Length != 10 || !Regex.IsMatch(model.ClientNationalCode, @"^\d{10}$"))
            {
                model.Step3Message = "کد ملی موکل نامعتبر است.";
                step3Valid = false;
            }
            else
            {
                model.Step3Message = "تطابق کد ملی معتبر است.";
            }

            // پیام نهایی
            if (step1Valid && step2Valid && step3Valid)
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


        public class UpdateValidationStatusModel
        {
            public long RequestId { get; set; }
            public bool ValidateByExpert { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateValidationStatus([FromBody] UpdateValidationStatusModel model)
        {
            try
            {
                _logger.LogInformation("Received UpdateValidationStatus for RequestId: {RequestId}", model.RequestId);

                var token = HttpContext.Session.GetString("JwtToken") ?? ViewBag.JwtToken;
                if (string.IsNullOrEmpty(token))
                    return Json(new { success = false, message = "توکن یافت نشد. لطفاً دوباره وارد سیستم شوید." });

                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var content = new StringContent(
                    JsonSerializer.Serialize(model),
                    Encoding.UTF8,
                    "application/json"
                );

                // حتماً api/ داشته باشد
                var response = await _client.PostAsync($"Request/UpdateValidationStatus", content);

                var responseContent = await response.Content.ReadAsStringAsync();

                // این خط مهم است: مستقیماً JSON API را برگردان
                if (response.IsSuccessStatusCode)
                {
                    return Content(responseContent, "application/json");
                }
                else
                {
                    // فقط پیام API را برگردان (بدون بسته‌بندی دوباره)
                    try
                    {
                        var errorObj = JsonSerializer.Deserialize<JsonElement>(responseContent);
                        if (errorObj.TryGetProperty("message", out var msg))
                        {
                            return Json(new { success = false, message = msg.GetString() });
                        }
                    }
                    catch { }

                    return Json(new { success = false, message = "خطا در ارتباط با سرور." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateValidationStatus");
                return Json(new { success = false, message = "خطا در ارتباط با سرور." });
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
                    var apiResponse = await response.Content.ReadFromJsonAsync<DocTextResponse>();
                    return Json(new { success = apiResponse.Success, documentText = apiResponse.DocumentText, isRead = apiResponse.IsRead });
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

        [HttpGet]
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
                if (string.IsNullOrEmpty(model.NationalCode) || model.NationalCode.Length != 10 || !System.Text.RegularExpressions.Regex.IsMatch(model.NationalCode, @"^\d{10}$"))
                {
                    return Json(new { success = false, message = "کد ملی نامعتبر است." });
                }
                if (string.IsNullOrEmpty(model.MobileNumber) || model.MobileNumber.Length != 11 || !System.Text.RegularExpressions.Regex.IsMatch(model.MobileNumber, @"^09\d{9}$"))
                {
                    return Json(new { success = false, message = "شماره موبایل نامعتبر است." });
                }

                var token = HttpContext.Session.GetString("JwtToken") ?? ViewBag.JwtToken;
                if (string.IsNullOrEmpty(token))
                {
                    return Json(new { success = false, message = "لطفاً ابتدا وارد سیستم شوید." });
                }
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

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

        #endregion

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

    #region Helper
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

    #endregion

    #region ViewModels
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

    #endregion

}