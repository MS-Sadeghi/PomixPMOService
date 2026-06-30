using IdentityManagementSystem.UI.ViewModels;

namespace IdentityManagementSystem.API.Services.AccessControlReports
{
    public interface IAccessControlReportService
    {
        Task<List<GetDataReportViewModel>> GetDataAsync(GetDataFilterViewModel filter);
    }
}
