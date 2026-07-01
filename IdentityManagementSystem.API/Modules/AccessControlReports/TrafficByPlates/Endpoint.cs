using Microsoft.AspNetCore.Mvc;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.TrafficByPlates
{
    [ApiController]
    [Route("api/access-control-reports/traffic-by-plates")]
    public class Endpoint : ControllerBase
    {
        private readonly TrafficByPlatesHandler _handler;

        public Endpoint(TrafficByPlatesHandler handler)
        {
            _handler = handler;
        }

        [HttpPost]
        public async Task<IActionResult> Execute(TrafficByPlatesRequest request)
        {
            var result = await _handler.HandleAsync(request);

            return Ok(result);
        }
    }
}
