using IdentityManagementSystem.UI.ViewModels;

namespace IdentityManagementSystem.API.Services.AccessControlReports
{
    public interface IAccessControlReportService
    {
        Task<List<GetDataReportViewModel>> GetDataAsync(GetDataFilterViewModel filter);
        Task<List<GetSumReportViewModel>> GetSumAsync(BaseReportFilterViewModel filter);
        Task<List<TrafficByTypeReportViewModel>> TrafficByTypeAsync(TrafficByTypeFilterViewModel filter);
        Task<List<TrafficByPlatesReportViewModel>> TrafficByPlatesAsync(TrafficByPlatesFilterViewModel filter);
        Task<List<TrafficByNationalIdReportViewModel>> TrafficByNationalIdAsync(TrafficByNationalIdFilterViewModel filter);
		Task<DashboardResponseViewModel> GetDashboardAsync();
	}
}
