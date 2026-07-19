namespace IdentityManagementSystem.API.Modules.AccessControlReports.TrafficByNationalId
{
    public class TrafficByNationalIdResponse
    {
        public int RowNumber { get; set; }

        public string FullName { get; set; }

        public string NationalId { get; set; }

        public string LogDateTimePersian { get; set; }

        public string EntranceType { get; set; }
    }
}
