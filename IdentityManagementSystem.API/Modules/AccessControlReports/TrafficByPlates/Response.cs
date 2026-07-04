namespace IdentityManagementSystem.API.Modules.AccessControlReports.TrafficByPlates
{
    public class TrafficByPlatesResponse
    {
        public int RowNumber { get; set; }

        public string PlateNumber { get; set; }

        public string LogDateTime { get; set; }

        public string EntranceType { get; set; }
    }
}
