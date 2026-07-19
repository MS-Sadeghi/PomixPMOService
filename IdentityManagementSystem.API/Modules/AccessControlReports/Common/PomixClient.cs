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

        var responseText = await response.Content.ReadAsStringAsync();

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

        return result;
    }
}