namespace IdentityManagementSystem.API.Modules.AccessControlReports.TrafficByNationalId
{
    public class TrafficByNationalIdRequest
    {
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string NationalId { get; set; }
    }
}
