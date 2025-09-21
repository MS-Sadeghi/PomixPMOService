using PomixPMOService.UI.ViewModels;

namespace PomixPMOService.UI.Service
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;

        public ApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("http://localhost:5000/api/"); // آدرس API
        }

        // کاربران
        public async Task<List<UserViewModel>> GetUsersAsync()
        {
            return await _httpClient.GetFromJsonAsync<List<UserViewModel>>("users");
        }

        public async Task<UserViewModel> GetUserAsync(long id)
        {
            return await _httpClient.GetFromJsonAsync<UserViewModel>($"users/{id}");
        }

        public async Task CreateUserAsync(CreateUserViewModel viewModel)
        {
            await _httpClient.PostAsJsonAsync("users", viewModel);
        }

        // درخواست‌ها
        public async Task<List<RequestViewModel>> GetRequestsAsync()
        {
            return await _httpClient.GetFromJsonAsync<List<RequestViewModel>>("requests");
        }

        public async Task<RequestViewModel> GetRequestAsync(long id)
        {
            return await _httpClient.GetFromJsonAsync<RequestViewModel>($"requests/{id}");
        }

        public async Task CreateRequestAsync(CreateRequestViewModel viewModel)
        {
            await _httpClient.PostAsJsonAsync("requests", viewModel);
        }

        // کارتابل
        public async Task<List<CartableItemViewModel>> GetCartableItemsAsync(long userId)
        {
            return await _httpClient.GetFromJsonAsync<List<CartableItemViewModel>>($"cartables/user/{userId}");
        }

        public async Task AssignCartableItemAsync(AssignCartableItemViewModel viewModel)
        {
            await _httpClient.PostAsJsonAsync("cartables/assign", viewModel);
        }
    }
}