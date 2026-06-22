using System.ComponentModel.DataAnnotations;

namespace IdentityManagementSystem.UI.ViewModels
{
    #region Common Filter

    public class BaseAccessReportFilterViewModel
    {
        public string StartDate { get; set; }

        public string EndDate { get; set; }

        public string StartTime { get; set; } = "00:00";

        public string EndTime { get; set; } = "23:59";
    }

    #endregion

    #region GetData

    public class GetDataFilterViewModel : BaseAccessReportFilterViewModel
    {
        public List<string> EntranceTypes { get; set; } = new();
    }

    public class GetDataReportViewModel
    {
        public string ReportDate { get; set; }

        public string EntranceType { get; set; }

        public int RecordCount { get; set; }
    }

    public class GetDataReportPageViewModel
    {
        public GetDataFilterViewModel Filter { get; set; } = new();

        public List<GetDataReportViewModel> Reports { get; set; } = new();
    }

    #endregion

    #region GetSum

    public class GetSumReportViewModel
    {
        public string ReportDate { get; set; }

        public string EntranceType { get; set; }

        public int RecordCount { get; set; }
    }

    public class GetSumReportPageViewModel
    {
        public BaseAccessReportFilterViewModel Filter { get; set; } = new();

        public List<GetSumReportViewModel> Reports { get; set; } = new();
    }

    #endregion

    #region TrafficByType

    public class TrafficByTypeFilterViewModel : BaseAccessReportFilterViewModel
    {
        public List<int> TrafficTypes { get; set; } = new();
    }

    public class TrafficByTypeReportViewModel
    {
        public string ReportDate { get; set; }

        public string EntranceType { get; set; }

        public int RecordCount { get; set; }
    }

    public class TrafficByTypePageViewModel
    {
        public TrafficByTypeFilterViewModel Filter { get; set; } = new();

        public List<TrafficByTypeReportViewModel> Reports { get; set; } = new();
    }

    #endregion

    #region TrafficByPlates

    public class TrafficByPlatesFilterViewModel : BaseAccessReportFilterViewModel
    {
        public string P1 { get; set; }

        public string P2 { get; set; }

        public string P3 { get; set; }

        public string P4 { get; set; }
    }

    public class TrafficByPlatesReportViewModel
    {
        public int RowNumber { get; set; }

        public string PlateNumber { get; set; }

        public string LogDateTime { get; set; }

        public string EntranceType { get; set; }
    }

    public class TrafficByPlatesPageViewModel
    {
        public TrafficByPlatesFilterViewModel Filter { get; set; } = new();

        public List<TrafficByPlatesReportViewModel> Reports { get; set; } = new();
    }

    #endregion

    #region TrafficByNationalId

    public class TrafficByNationalIdFilterViewModel : BaseAccessReportFilterViewModel
    {
        public string NationalId { get; set; }
    }

    public class TrafficByNationalIdReportViewModel
    {
        public int RowNumber { get; set; }

        public string FullName { get; set; }

        [Required(ErrorMessage = "کد ملی الزامی است")]
        [StringLength(10, MinimumLength = 10,
        ErrorMessage = "کد ملی باید 10 رقم باشد")]
        public string NationalId { get; set; }

        public string LogDateTimePersian { get; set; }

        public string EntranceType { get; set; }
    }

    public class TrafficByNationalIdPageViewModel
    {
        public TrafficByNationalIdFilterViewModel Filter { get; set; } = new();

        public List<TrafficByNationalIdReportViewModel> Reports { get; set; } = new();
    }

    #endregion
}
