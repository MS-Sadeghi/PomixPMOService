using Microsoft.AspNetCore.Mvc;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.AllTraffic
{
    [ApiController]
    [Route("api/access-control-reports/all-traffic")]
    public class Endpoint : ControllerBase
    {
        private readonly AllTrafficHandler _handler;

        public Endpoint(AllTrafficHandler handler)
        {
            _handler = handler;
        }

        [HttpPost]
        public async Task<IActionResult> Execute(AllTrafficRequest request)
        {
            var result = await _handler.HandleAsync(request);

            return Ok(result);
        }
    }
}
