using IdentityManagementSystem.UI.Areas.AccessControlReports.ViewModel;
using IdentityManagementSystem.UI.ViewModels;

namespace IdentityManagementSystem.UI.Areas.AccessControlReports.Services
{
    public interface IAccessControlReportService
    {
        Task<List<GetDataReportViewModel>> GetDataAsync(GetDataFilterViewModel filter);
        Task<List<GetSumReportViewModel>> GetSumAsync(BaseReportFilterViewModel filter);
        Task<List<TrafficByTypeReportViewModel>> TrafficByTypeAsync(TrafficByTypeFilterViewModel filter);
        Task<List<TrafficByPlatesReportViewModel>> TrafficByPlatesAsync(TrafficByPlatesFilterViewModel filter);
        Task<List<TrafficByNationalIdReportViewModel>> TrafficByNationalIdAsync(TrafficByNationalIdFilterViewModel filter);
		Task<DashboardResponseViewModel> GetDashboardAsync(string period);
	}
}
