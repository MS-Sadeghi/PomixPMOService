using Microsoft.AspNetCore.Mvc;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.Dashboard
{
	[ApiController]
	[Route("api/access-control-reports/dashboard")]
	public class Endpoint : ControllerBase
	{
		private readonly DashboardHandler _handler;

		public Endpoint(DashboardHandler handler)
		{
			_handler = handler;
		}

		[HttpPost]
		public async Task<IActionResult> Execute([FromBody] DashboardRequest? request)
		{
			var result = await _handler.HandleAsync(request ?? new DashboardRequest());

			return Ok(result);
		}
	}
}