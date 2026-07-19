using IdentityManagementSystem.API.Modules.AccessControlReports.Common;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.TrafficByPlates
{
    public class TrafficByPlatesHandler
    {
        private readonly IPomixClient _pomixClient;
        private readonly IConfiguration _configuration;

        public TrafficByPlatesHandler(IPomixClient pomixClient, IConfiguration configuration)
        {
            _pomixClient = pomixClient;
            _configuration = configuration;
        }

        public async Task<List<TrafficByPlatesResponse>> HandleAsync(TrafficByPlatesRequest request)
        {
            var parameters = new object[]
            {
                new
                {
                    parameterName = "startDate",
                    parameterValue = ToEnglishDigits(request.StartDate)
                },
                new
                {
                    parameterName = "endDate",
                    parameterValue = ToEnglishDigits(request.EndDate)
                },
                new
                {
                    parameterName = "p1",
                    parameterValue = ToEnglishDigits(request.P1)
                },
                new
                {
                    parameterName = "p2",
                    parameterValue = request.P2
                },
                new
                {
                    parameterName = "p3",
                    parameterValue = ToEnglishDigits(request.P3)
                },
                new
                {
                    parameterName = "p4",
                    parameterValue = ToEnglishDigits(request.P4)
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

            return await _pomixClient.ExecuteAsync<List<TrafficByPlatesResponse>>(
                "bsr-TrafficByPlates",
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
