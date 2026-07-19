namespace IdentityManagementSystem.API.Modules.AccessControlReports.Dashboard
{
	public class DashboardResponse
	{
		public int TotalTrafficToday { get; set; }
		public int VehicleTrafficToday { get; set; }
		public int PeopleTrafficToday { get; set; }
		public int ReportsCountToday { get; set; }

		public List<ChartItemResponse> WeeklyTraffic { get; set; } = new();
		public List<ChartItemResponse> TrafficTypes { get; set; } = new();

		public string LastUpdated { get; set; } = string.Empty;
	}

	public class ChartItemResponse
	{
		public string Label { get; set; } = string.Empty;
		public int Value { get; set; }
	}
}