namespace IdentityManagementSystem.UI.ViewModels
{
	public class DashboardResponseViewModel
	{
		public string Period { get; set; } = "today";

		public int TotalTrafficToday { get; set; }
		public int VehicleTrafficToday { get; set; }
		public int PeopleTrafficToday { get; set; }
		public int ReportsCountToday { get; set; }

		public List<DashboardChartItemViewModel> WeeklyTraffic { get; set; } = new();
		public List<DashboardChartItemViewModel> TrafficTypes { get; set; } = new();

		public string LastUpdated { get; set; } = string.Empty;
	}

	public class DashboardChartItemViewModel
	{
		public string Label { get; set; } = string.Empty;
		public int Value { get; set; }
	}
}