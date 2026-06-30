using Microsoft.Extensions.Options;
using System.Text.Json;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.Common;

public class PomixClient : IPomixClient
{
    private readonly HttpClient _httpClient;
    private readonly PomixOptions _options;

    public PomixClient(
        HttpClient httpClient,
        IOptions<PomixOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
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

        var response = await _httpClient.PostAsJsonAsync("", request);

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content.ReadFromJsonAsync<PomixResponse>();

        if (result == null)
            throw new Exception("No response received from Pomix.");

        if (!result.IsSuccessful)
            throw new Exception(result.ErrorDescription);

        return JsonSerializer.Deserialize<T>(
            result.ResponseText,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;
    }
}