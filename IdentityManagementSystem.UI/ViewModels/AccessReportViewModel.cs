using Microsoft.AspNetCore.Mvc;

namespace IdentityManagementSystem.UI.ViewModels
{
    public class AccessReportViewModel
    {
        public string ReportDate { get; set; }
        public string EntranceType { get; set; }
        public int RecordCount { get; set; }
    }
}
