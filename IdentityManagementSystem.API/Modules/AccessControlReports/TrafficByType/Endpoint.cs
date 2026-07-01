using Microsoft.AspNetCore.Mvc;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.TrafficByType
{
    [ApiController]
    [Route("api/access-control-reports/traffic-by-type")]
    public class Endpoint : ControllerBase
    {
        private readonly TrafficByTypeHandler _handler;

        public Endpoint(TrafficByTypeHandler handler)
        {
            _handler = handler;
        }

        [HttpPost]
        public async Task<IActionResult> Execute(TrafficByTypeRequest request)
        {
            var result = await _handler.HandleAsync(request);

            return Ok(result);
        }
    }
}
