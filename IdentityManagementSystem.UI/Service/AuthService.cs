public class AuthService
{
    private readonly HttpClient _httpClient;

    public AuthService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> ValidateUserAsync(string username, string password)
    {
        var loginModel = new
        {
            Username = username,
            Password = password
        };

        var response = await _httpClient.PostAsJsonAsync("Auth/login", loginModel);

        if (!response.IsSuccessStatusCode)
            return false;

        var token = await response.Content.ReadAsStringAsync();

        // اینجا باید session ذخیره شود (در controller نه service بهتره)
        return true;
    }
}