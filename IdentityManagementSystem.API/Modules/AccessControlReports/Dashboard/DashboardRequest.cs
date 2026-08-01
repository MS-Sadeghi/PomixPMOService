using Microsoft.AspNetCore.Mvc;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.Dashboard
{
	public class DashboardRequest
	{
		// مقادیر مجاز: today, yesterday, last7, last30
		public string Period { get; set; } = "today";

		// وقتی true باشد، کش نادیده گرفته می‌شود و از Pomix داده‌ی تازه گرفته می‌شود
		public bool ForceRefresh { get; set; }
	}
}