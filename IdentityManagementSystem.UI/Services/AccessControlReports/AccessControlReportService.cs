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

            var responseContent =
                    await response.Content.ReadAsStringAsync();

            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new List<GetDataReportViewModel>();

            return await response.Content.ReadFromJsonAsync<List<GetDataReportViewModel>>()
                   ?? new List<GetDataReportViewModel>();
        }

        public async Task<List<GetSumReportViewModel>> GetSumAsync(BaseReportFilterViewModel filter)
        {
            var client = _factory.CreateClient("PomixApi");

            var request = new
            {
                StartDate = filter.StartDate,
                EndDate = filter.EndDate,
                StartTime = filter.StartTime,
                EndTime = filter.EndTime
            };

            var response = await client.PostAsJsonAsync(
                "access-control-reports/get-sum",
                request);

            if (!response.IsSuccessStatusCode)
                return new List<GetSumReportViewModel>();

            return await response.Content.ReadFromJsonAsync<List<GetSumReportViewModel>>()
                   ?? new List<GetSumReportViewModel>();
        }

        public async Task<List<TrafficByTypeReportViewModel>> TrafficByTypeAsync(TrafficByTypeFilterViewModel filter)
        {
            var client = _factory.CreateClient("PomixApi");

            var request = new
            {
                StartDate = filter.StartDate,
                EndDate = filter.EndDate,
                StartTime = filter.StartTime,
                EndTime = filter.EndTime,
                TrafficTypes = filter.TrafficTypes
            };

            var response = await client.PostAsJsonAsync(
                "access-control-reports/traffic-by-type",
                request);

            if (!response.IsSuccessStatusCode)
                return new List<TrafficByTypeReportViewModel>();

            return await response.Content.ReadFromJsonAsync<List<TrafficByTypeReportViewModel>>()
                   ?? new List<TrafficByTypeReportViewModel>();
        }

        public async Task<List<TrafficByPlatesReportViewModel>> TrafficByPlatesAsync(TrafficByPlatesFilterViewModel filter)
        {
            var client = _factory.CreateClient("PomixApi");

            var request = new
            {
                StartDate = filter.StartDate,
                EndDate = filter.EndDate,
                StartTime = filter.StartTime,
                EndTime = filter.EndTime,
                P1 = filter.P1,
                P2 = filter.P2,
                P3 = filter.P3,
                P4 = filter.P4
            };

            var response = await client.PostAsJsonAsync(
                "access-control-reports/traffic-by-plates",
                request);

            if (!response.IsSuccessStatusCode)
                return new List<TrafficByPlatesReportViewModel>();

            return await response.Content.ReadFromJsonAsync<List<TrafficByPlatesReportViewModel>>()
                   ?? new List<TrafficByPlatesReportViewModel>();
        }

        public async Task<List<TrafficByNationalIdReportViewModel>> TrafficByNationalIdAsync(TrafficByNationalIdFilterViewModel filter)
        {
            var client = _factory.CreateClient("PomixApi");

            var request = new
            {
                StartDate = filter.StartDate,
                EndDate = filter.EndDate,
                StartTime = filter.StartTime,
                EndTime = filter.EndTime,
                NationalId = filter.NationalId
            };

            var response = await client.PostAsJsonAsync(
                "access-control-reports/traffic-by-nationalid",
                request);

            if (!response.IsSuccessStatusCode)
                return new List<TrafficByNationalIdReportViewModel>();

            return await response.Content.ReadFromJsonAsync<List<TrafficByNationalIdReportViewModel>>()
                   ?? new List<TrafficByNationalIdReportViewModel>();
        }
    }
}
