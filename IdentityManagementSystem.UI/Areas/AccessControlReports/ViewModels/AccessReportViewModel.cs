using System.ComponentModel.DataAnnotations;

namespace IdentityManagementSystem.UI.Areas.AccessControlReports.ViewModel
{
    #region Common Filter

    public class BaseReportFilterViewModel
    {
        public string StartDate { get; set; }

        public string EndDate { get; set; }

        public string StartTime { get; set; } = "00:00";

        public string EndTime { get; set; } = "23:59";
    }

    #endregion

    #region GetData

    public class GetDataFilterViewModel : BaseReportFilterViewModel
    {
        public List<string> EntranceTypes { get; set; } = new();
    }

    public class GetDataReportViewModel
    {
        public string ReportDate { get; set; }

        public string EntranceType { get; set; }

        public int Transit { get; set; }

        public int IranianTruck { get; set; }

        public int IranianPassenger { get; set; }

        public int RecordCount => Transit + IranianTruck + IranianPassenger;
    }

    public class GetDataReportPageViewModel
    {
        public GetDataFilterViewModel Filter { get; set; } = new();

        public List<GetDataReportViewModel> Reports { get; set; } = new();
    }

    #endregion

    #region GetSum

    public class GetSumCategoryStatViewModel
    {
        public int Total { get; set; }

        public int? Iranian { get; set; }

        public int? Transit { get; set; }
    }

    public class GetSumDailyStatViewModel
    {
        public string ReportDate { get; set; }

        public GetSumCategoryStatViewModel TruckEntrance { get; set; }

        public GetSumCategoryStatViewModel TruckEmptyExit { get; set; }

        public GetSumCategoryStatViewModel TruckLoadedExit { get; set; }

        public GetSumCategoryStatViewModel CarEntrance { get; set; }

        public GetSumCategoryStatViewModel CarExit { get; set; }

        public GetSumCategoryStatViewModel PedestrianEntrance { get; set; }

        public GetSumCategoryStatViewModel PedestrianExit { get; set; }
    }

    public class GetSumReportViewModel
    {
        public List<GetSumDailyStatViewModel> Daily { get; set; } = new();

        public GetSumDailyStatViewModel TotalPeriod { get; set; }
    }

    public class GetSumReportPageViewModel
    {
        public BaseReportFilterViewModel Filter { get; set; } = new();

        public GetSumReportViewModel Reports { get; set; } = new();
    }

    #endregion

    #region TrafficByType

    public class TrafficByTypeFilterViewModel : BaseReportFilterViewModel
    {
        public List<int> TrafficTypes { get; set; } = new();
    }

    public class TrafficByTypeReportViewModel
    {
        public string ReportDate { get; set; }

        public string EntranceType { get; set; }

        public int Transit { get; set; }

        public int IranianTruck { get; set; }

        public int IranianPassenger { get; set; }

        public int RecordCount => Transit + IranianTruck + IranianPassenger;
    }

    public class TrafficByTypePageViewModel
    {
        public TrafficByTypeFilterViewModel Filter { get; set; } = new();

        public List<TrafficByTypeReportViewModel> Reports { get; set; } = new();
    }

    #endregion

    #region TrafficByPlates

    public class TrafficByPlatesFilterViewModel : BaseReportFilterViewModel
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

    public class TrafficByNationalIdFilterViewModel : BaseReportFilterViewModel
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
