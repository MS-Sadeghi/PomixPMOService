using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;
using IdentityManagementSystem.API.Models;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.Common;

public class PomixClient : IPomixClient
{
    private readonly HttpClient _httpClient;
    private readonly PomixOptions _options;
    private readonly ServiceCallLogger _callLogger;

    public PomixClient(
        HttpClient httpClient,
        IOptions<PomixOptions> options,
        ServiceCallLogger callLogger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _callLogger = callLogger;
    }

    public async Task<T> ExecuteAsync<T>(
    string serviceName,
    object[] parameters)
    {
        var request = new
        {
            credential = new
            {
                code = _options.Code,
                password = _options.Password
            },
            parameters,
            service = serviceName
        };

        // مرحله ۱: «ارسال شد» قبل از رفتن درخواست ثبت می‌شود؛
        // اگر سرویس جواب ندهد همین رکورد با Stage = Sent باقی می‌ماند.
        var logId = await _callLogger.StartAsync(serviceName, _options.BaseUrl, parameters);
        var stopwatch = Stopwatch.StartNew();

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync("", request);
        }
        catch (TaskCanceledException)
        {
            // انقضای زمان انتظار (HttpClient.Timeout) - سرویس Pomix پاسخ نداده است
            var message = $"Pomix service '{serviceName}' did not respond within {_httpClient.Timeout.TotalSeconds:0} seconds.";
            await _callLogger.FinishAsync(logId, ServiceCallLog.StageTimedOut, (int)stopwatch.ElapsedMilliseconds, errorMessage: message);
            throw new TimeoutException(message);
        }
        catch (Exception ex)
        {
            // خطای شبکه / اتصال (DNS، قطع اتصال، SSL و ...)
            await _callLogger.FinishAsync(logId, ServiceCallLog.StageFailed, (int)stopwatch.ElapsedMilliseconds, errorMessage: ex.Message);
            throw;
        }

        var responseText = await response.Content.ReadAsStringAsync();
        var httpStatus = (int)response.StatusCode;
        int? pomixStatus = null;

        try
        {
            var pomixResult =
                JsonSerializer.Deserialize<PomixResult>(
                    responseText,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (pomixResult == null)
            {
                throw new Exception("Pomix response is null");
            }

            pomixStatus = pomixResult.ResponseStatusCode;

            if (pomixResult.ResponseStatusCode != 200)
            {
                throw new Exception(pomixResult.ResponseText);
            }

            var result =
                JsonSerializer.Deserialize<T>(
                    pomixResult.ResponseText,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (result == null)
            {
                throw new Exception("Unable to deserialize response.");
            }

            await _callLogger.FinishAsync(logId, ServiceCallLog.StageCompleted, (int)stopwatch.ElapsedMilliseconds,
                httpStatus, pomixStatus, responseText.Length);

            return result;
        }
        catch (Exception ex)
        {
            await _callLogger.FinishAsync(logId, ServiceCallLog.StageFailed, (int)stopwatch.ElapsedMilliseconds,
                httpStatus, pomixStatus, responseText.Length, ex.Message);
            throw;
        }
    }
}
