using IdentityManagementSystem.API.Modules.AccessControlReports.GetData;
using IdentityManagementSystem.API.Modules.AccessControlReports.TrafficByNationalId;
using IdentityManagementSystem.API.Modules.AccessControlReports.TrafficByPlates;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.AllTraffic
{
    public class AllTrafficResponse
    {
        // "types" | "lanes" | "plates" | "nationalid"
        public string Mode { get; set; } = "types";

        public List<AllTrafficDailyRow> DailyRows { get; set; } = new();
        public List<GetDataResponse> LaneRows { get; set; } = new();
        public List<TrafficByPlatesResponse> PlateRows { get; set; } = new();
        public List<TrafficByNationalIdResponse> NationalIdRows { get; set; } = new();
    }

    public class AllTrafficDailyRow
    {
        public string ReportDate { get; set; }
        public int CarCount { get; set; }
        public int TruckCount { get; set; }
        public int PedestrianCount { get; set; }
        public int Total { get; set; }
    }
}
