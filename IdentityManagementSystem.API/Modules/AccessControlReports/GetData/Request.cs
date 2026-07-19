namespace IdentityManagementSystem.API.Modules.AccessControlReports.GetData
{
    public class GetDataRequest
    {
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }

        public List<string> EntranceTypes { get; set; }
    }
}
