using Microsoft.AspNetCore.Mvc;

namespace IdentityManagementSystem.UI.ViewModels
{
    public class AccessReportViewModel
    {
        public string ReportDate { get; set; }
        public string EntranceType { get; set; }
        public int RecordCount { get; set; }
    }

    public class AccessReportFilterViewModel
    {
        public string StartDate { get; set; }

        public string EndDate { get; set; }

        public string StartTime { get; set; } = "00:00";

        public string EndTime { get; set; } = "23:59";

        public List<string> EntranceTypes { get; set; } = new();
    }

    public class AccessReportPageViewModel
    {
        public AccessReportFilterViewModel Filter { get; set; } = new();

        public List<AccessReportViewModel> Reports { get; set; } = new();
    }
}
