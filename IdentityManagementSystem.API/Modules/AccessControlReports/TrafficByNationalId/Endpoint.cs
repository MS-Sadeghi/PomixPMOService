using Microsoft.AspNetCore.Mvc;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.TrafficByNationalId
{
    [ApiController]
    [Route("api/access-control-reports/traffic-by-nationalid")]
    public class Endpoint : ControllerBase
    {
        private readonly TrafficByNationalIdHandler _handler;

        public Endpoint(TrafficByNationalIdHandler handler)
        {
            _handler = handler;
        }

        [HttpPost]
        public async Task<IActionResult> Execute(TrafficByNationalIdRequest request)
        {
            var result = await _handler.HandleAsync(request);

            return Ok(result);
        }
    }
}
