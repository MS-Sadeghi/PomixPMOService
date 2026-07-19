using Microsoft.AspNetCore.Mvc;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.GetData
{
    [ApiController]
    [Route("api/access-control-reports/get-data")]
    public class Endpoint : ControllerBase
    {
        private readonly GetDataHandler _handler;

        public Endpoint(GetDataHandler handler)
        {
            _handler = handler;
        }

        [HttpPost]
        public async Task<IActionResult> Execute(
            GetDataRequest request)
        {
            var result =
                await _handler.HandleAsync(request);

            return Ok(result);
        }
    }
}
