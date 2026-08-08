using IdentityManagementSystem.API.Data;
using IdentityManagementSystem.API.Models;
using IdentityManagementSystem.API.Services.SMS.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Web;

namespace IdentityManagementSystem.API.Services.SMS
{
    public class SendSmsService
    {
        private readonly HttpClient _httpClient;
        private readonly IdentityManagementSystemContext _context;
        private readonly ILogger<SendSmsService> _logger;
        private readonly DiafaanSmsSettings _diafaanSmsSettings;
        private readonly RajaeiSmsSettings _rajaeiSmsSettings;
        private readonly SendServiceClient _sendServiceClient;
        private readonly IServiceProvider _serviceProvider;

        public SendSmsService(
            HttpClient httpClient,
            IdentityManagementSystemContext context,
            ILogger<SendSmsService> logger,
            IOptions<DiafaanSmsSettings> diafaanOptions,
            IOptions<RajaeiSmsSettings> rajaeiOptions,
            SendServiceClient sendServiceClient,
            IServiceProvider serviceProvider)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _context = context;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _diafaanSmsSettings = diafaanOptions?.Value ?? throw new ArgumentNullException(nameof(diafaanOptions));
            _rajaeiSmsSettings = rajaeiOptions?.Value ?? throw new ArgumentNullException(nameof(rajaeiOptions));
            _sendServiceClient = sendServiceClient ?? throw new ArgumentNullException(nameof(sendServiceClient));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task<bool> SendSmsViaDiafaanAsync(SendSmsViewModel model)
        {
            try
            {
                if (model == null) throw new ArgumentNullException(nameof(model));

                var request = new SendSMSRequest(
                    _diafaanSmsSettings.Username,
                    _diafaanSmsSettings.Password,
                    new[] { model.Mobile },
                    model.Text);

                var result = await _sendServiceClient.SendSMSAsync(request);
                bool isSuccess = result.SendSMSResult == "0";

                var log = new SmsLog
                {
                    Id = Guid.NewGuid(),
                    Area = model.Area,
                    Controller = model.Controller,
                    Action = model.Action,
                    Mobile = model.Mobile,
                    Text = model.LogText,
                    ResultCode = result.SendSMSResult,
                    IsSend = isSuccess,
                    UserId = model.UserId,
                    Date = DateTime.Now,
                    ResultDesc = result.SendSMSResult
                };

                string userId = model.UserId?.ToString() ?? "System";
                await _context.SmsLogs.AddAsync(log);
                await _context.SaveChangesAsync();

                //await _auditLogger.LogAsync(userId, "SendSms", log, model.Mobile, $"ارسال از طریق Diafaan. نتیجه: {result.SendSMSResult}");

                return isSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Diafaan: خطا در ارسال پیامک به {Mobile}", model?.Mobile ?? "Unknown");
                //await _auditLogger.LogAsync(model?.UserId?.ToString() ?? "System", "SendSmsError", new { }, model?.Mobile ?? "Unknown", $"خطای Diafaan: {ex.Message}");
                return false;
            }
        }

        public async Task SendSmsToGroupViaDiafaanAsync(List<User> receivers, string notifyText, string area, string controller, string action)
        {
            foreach (var receiver in receivers.Where(u => !string.IsNullOrWhiteSpace(u.MobileNumber)))
            {
                try
                {
                    var model = new SendSmsViewModel
                    {
                        Mobile = receiver.MobileNumber,
                        Text = notifyText,
                        Area = area,
                        Controller = controller,
                        Action = action,
                        LogText = notifyText,
                        UserId = receiver.UserId
                    };

                    await SendSmsViaDiafaanAsync(model);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "خطا در ارسال گروهی Diafaan به {UserId}", receiver.UserId);
                }
            }
        }

        public async Task<bool> SendSmsAsync(SendSmsViewModel model)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IdentityManagementSystemContext>();
                try
                {
                    if (model == null)
                    {
                        throw new ArgumentNullException(nameof(model));
                    }

                    var queryParams = HttpUtility.ParseQueryString(string.Empty);
                    queryParams["appName"] = _rajaeiSmsSettings.AppName;
                    queryParams["to"] = HttpUtility.UrlEncode(model.Mobile);
                    queryParams["msg"] = model.Text;

                    if (string.IsNullOrWhiteSpace(_rajaeiSmsSettings.BaseUrl))
                    {
                        throw new InvalidOperationException("BaseUrl برای RajaeiSmsSettings به درستی تنظیم نشده است.");
                    }

                    string fullUrl = $"{_rajaeiSmsSettings.BaseUrl.TrimEnd('/')}?{queryParams}";
                    _logger.LogInformation("Rajaei - ارسال پیامک به {Mobile}: {Url}", model.Mobile, fullUrl);

                    var response = await _httpClient.GetAsync(fullUrl);
                    response.EnsureSuccessStatusCode();

                    string result = await response.Content.ReadAsStringAsync();
                    bool isSuccess = result.Contains("success", StringComparison.OrdinalIgnoreCase);

                    var log = new SmsLog
                    {
                        Id = Guid.NewGuid(),
                        Area = model.Area,
                        Controller = model.Controller,
                        Action = model.Action,
                        Mobile = model.Mobile,
                        Text = model.LogText,
                        ResultCode = isSuccess ? "0" : "1",
                        IsSend = true,
                        UserId = model.UserId,
                        Date = DateTime.Now,
                        ResultDesc = result
                    };

                    string userId = model.UserId?.ToString() ?? "System";
                    await unitOfWork.SmsLogs.AddAsync(log);
                    await unitOfWork.SaveChangesAsync();

                    //await _auditLogger.LogAsync(userId, "SendSms", log, model.Mobile, $"ارسال از طریق Rajaei. نتیجه: {result}");

                    return isSuccess;
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "خطای HTTP در ارسال پیامک به {Mobile}", model?.Mobile ?? "Unknown");
                    return false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطای عمومی در ارسال پیامک به {Mobile}", model?.Mobile ?? "Unknown");
                    return false;
                }
            }
        }

        public async Task<bool> SendSmsToGroupAsync(
         List<long> groupUserIds,
         string notifyText,
         string area,
         string controller,
         string action)
        {

            if (groupUserIds == null || !groupUserIds.Any())
                return true;

            var users = await _context.Users
                .Where(x =>
                    groupUserIds.Contains(x.UserId) &&
                    !string.IsNullOrWhiteSpace(x.MobileNumber)
                )
                .ToListAsync();

            if (!users.Any())
                return true;

            var tasks = users.Select(async receiver =>
            {
                try
                {
                    var model = new SendSmsViewModel
                    {
                        Mobile = receiver.MobileNumber!.Trim(),
                        Text = notifyText,
                        Area = area,
                        Controller = controller,
                        Action = action,
                        LogText = notifyText,
                        UserId = receiver.UserId
                    };

                    return await SendSmsAsync(model);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "خطا در ارسال پیامک به {UserId}: {Message}",
                        receiver.UserId,
                        ex.Message
                    );
                    return false;
                }
            });

            var results = await Task.WhenAll(tasks);
            return results.All(r => r);
        }

        public async Task<bool> SendSecurityMessageToFava(string msg)
        {
            var reciverGroup = await _context.Roles
                .FirstOrDefaultAsync(x => x.RoleName == "FavaSecurityMessageRecivers");

            if (reciverGroup == null)
                throw new Exception("FavaSecurityMessageRecivers تعریف نشده است");

            var groupUserIds = await _context.UserAccesses
                .Where(g => g.UserId == reciverGroup.RoleId)
                .Select(g => g.UserId)
                .ToListAsync();

            return await SendSmsToGroupAsync(
                groupUserIds,
                msg,
                "Security",
                "Account",
                "Login"
            );
        }

    }

    public class SendServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly DiafaanSmsSettings _smsSettings;
        private readonly ILogger<SendServiceClient> _logger;

        public SendServiceClient(HttpClient httpClient, IOptions<DiafaanSmsSettings> smsSettingsOptions, ILogger<SendServiceClient> logger)
        {
            _smsSettings = smsSettingsOptions?.Value ?? throw new ArgumentNullException(nameof(smsSettingsOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClient;
        }

        public async Task<SendSMSResponse> SendSMSAsync(SendSMSRequest request)
        {
            try
            {
                var queryString = HttpUtility.ParseQueryString(string.Empty);
                queryString["username"] = _smsSettings.Username;
                queryString["password"] = _smsSettings.Password;
                queryString["to"] = string.Join(",", request.To);
                queryString["message"] = request.Message;

                string fullUrl = $"{_smsSettings.BaseUrl.TrimEnd('/')}/http/send-message?{queryString}";

                _logger.LogInformation("Sending SMS to {To} with message: {Message}", request.To, request.Message);

                var response = await _httpClient.GetAsync(fullUrl);
                response.EnsureSuccessStatusCode();

                string resultContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("SMS sent successfully with result: {Result}", resultContent);
                return new SendSMSResponse { SendSMSResult = resultContent };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error while sending SMS to {To}", request.To);
                return new SendSMSResponse { SendSMSResult = "HttpError" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while sending SMS to {To}", request.To);
                return new SendSMSResponse { SendSMSResult = "UnknownError" };
            }
        }
    }

    public class DiafaanSmsSettings
    {
        public string BaseUrl { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class RajaeiSmsSettings
    {
        public string BaseUrl { get; set; }
        public string AppName { get; set; }
    }

    public class SendSMSRequest
    {
        public string Username { get; }
        public string Password { get; }
        public string[] To { get; }
        public string Message { get; }

        public SendSMSRequest(string username, string password, string[] to, string message)
        {
            Username = username;
            Password = password;
            To = to;
            Message = message;
        }
    }

    public class SendSMSResponse
    {
        public string SendSMSResult { get; set; }
    }
}
