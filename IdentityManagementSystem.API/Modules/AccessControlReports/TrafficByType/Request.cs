namespace IdentityManagementSystem.API.Modules.AccessControlReports.TrafficByType
{
    public class TrafficByTypeRequest
    {
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }

        public List<int> TrafficTypes { get; set; }
    }
}
