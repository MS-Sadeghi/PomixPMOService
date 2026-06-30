using IdentityManagementSystem.API.Modules.AccessControlReports.Common;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.GetData
{
    public class GetDataHandler
    {
        private readonly IPomixClient _pomixClient;
        private readonly IConfiguration _configuration;

        public GetDataHandler(
            IPomixClient pomixClient,
            IConfiguration configuration)
        {
            _pomixClient = pomixClient;
            _configuration = configuration;
        }

        public async Task<List<GetDataResponse>>
            HandleAsync(GetDataRequest request)
        {
            var parameters = new object[]
            {
            new
            {
                parameterName = "StartDate",
                parameterValue = request.StartDate
            },
            new
            {
                parameterName = "EndDate",
                parameterValue = request.EndDate
            },
            new
            {
                parameterName = "EntranceTypes",
                parameterValue = request.EntranceTypes
            },
            new
            {
                parameterName = "startTime",
                parameterValue = request.StartTime
            },
            new
            {
                parameterName = "endTime",
                parameterValue = request.EndTime
            },
            new
            {
                parameterName = "credentials",
                parameterValue = new
                {
                    username =
                        _configuration["AccessControl:Username"],

                    password =
                        _configuration["AccessControl:Password"]
                }
            }
            };

            return await _pomixClient.ExecuteAsync
                <List<GetDataResponse>>
                (
                    "bsr-GetData",
                    parameters
                );
        }
    }
}
