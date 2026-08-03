namespace IdentityManagementSystem.API.Modules.AccessControlReports.AllTraffic
{
    public class AllTrafficRequest
    {
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }

        public List<string> EntranceTypes { get; set; } = new();
        public List<int> TrafficTypes { get; set; } = new();

        public string P1 { get; set; }
        public string P2 { get; set; }
        public string P3 { get; set; }
        public string P4 { get; set; }

        public string NationalId { get; set; }
    }
}
