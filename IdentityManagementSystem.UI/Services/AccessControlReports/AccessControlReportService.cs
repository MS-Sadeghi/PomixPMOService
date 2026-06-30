namespace IdentityManagementSystem.API.Services.AccessControlReports
{
    using IdentityManagementSystem.UI.ViewModels;
    using System.Net.Http.Json;

    public class AccessControlReportService : IAccessControlReportService
    {
        private readonly IHttpClientFactory _factory;

        public AccessControlReportService(IHttpClientFactory factory)
        {
            _factory = factory;
        }

        public async Task<List<GetDataReportViewModel>> GetDataAsync(GetDataFilterViewModel filter)
        {
            var client = _factory.CreateClient("PomixApi");

            var request = new
            {
                StartDate = filter.StartDate,
                EndDate = filter.EndDate,
                StartTime = filter.StartTime,
                EndTime = filter.EndTime,
                EntranceTypes = filter.EntranceTypes
            };

            var response = await client.PostAsJsonAsync(
                "access-control-reports/get-data",
                request);

            if (!response.IsSuccessStatusCode)
                return new List<GetDataReportViewModel>();

            return await response.Content.ReadFromJsonAsync<List<GetDataReportViewModel>>()
                   ?? new List<GetDataReportViewModel>();
        }
    }
}
