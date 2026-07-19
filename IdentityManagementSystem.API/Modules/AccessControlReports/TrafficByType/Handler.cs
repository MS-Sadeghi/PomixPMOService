using IdentityManagementSystem.API.Modules.AccessControlReports.Common;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.TrafficByType
{
    public class TrafficByTypeHandler
    {
        private readonly IPomixClient _pomixClient;
        private readonly IConfiguration _configuration;

        public TrafficByTypeHandler(IPomixClient pomixClient, IConfiguration configuration)
        {
            _pomixClient = pomixClient;
            _configuration = configuration;
        }

        public async Task<List<TrafficByTypeResponse>> HandleAsync(TrafficByTypeRequest request)
        {
            var parameters = new object[]
            {
                new
                {
                    parameterName = "StartDate",
                    parameterValue = ToEnglishDigits(request.StartDate)
                },
                new
                {
                    parameterName = "EndDate",
                    parameterValue = ToEnglishDigits(request.EndDate)
                },
                new
                {
                    parameterName = "TrafficTypes",
                    parameterValue = request.TrafficTypes
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
                        username = _configuration["AccessControl:Username"],
                        password = _configuration["AccessControl:Password"]
                    }
                }
            };

            return await _pomixClient.ExecuteAsync<List<TrafficByTypeResponse>>(
                "bsr-TrafficByType",
                parameters);
        }

        private static string ToEnglishDigits(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return input
                .Replace('۰', '0')
                .Replace('۱', '1')
                .Replace('۲', '2')
                .Replace('۳', '3')
                .Replace('۴', '4')
                .Replace('۵', '5')
                .Replace('۶', '6')
                .Replace('۷', '7')
                .Replace('۸', '8')
                .Replace('۹', '9');
        }
    }
}
