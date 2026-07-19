using Microsoft.AspNetCore.Mvc;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.GetSum
{
    [ApiController]
    [Route("api/access-control-reports/get-sum")]
    public class Endpoint : ControllerBase
    {
        private readonly GetSumHandler _handler;

        public Endpoint(GetSumHandler handler)
        {
            _handler = handler;
        }

        [HttpPost]
        public async Task<IActionResult> Execute(GetSumRequest request)
        {
            var result = await _handler.HandleAsync(request);

            return Ok(result);
        }
    }
}
