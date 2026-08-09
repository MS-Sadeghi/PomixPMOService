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
                    parameterValue = ToEnglishDigits(request.StartDate)
                },
                new
                {
                    parameterName = "EndDate",
                    parameterValue = ToEnglishDigits(request.EndDate)
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

            var raw = await _pomixClient.ExecuteAsync<GetDataRawResponse>(
                "bsr-GetData",
                parameters
            );

            return Flatten(raw);
        }

        internal static List<GetDataResponse> Flatten(GetDataRawResponse raw)
        {
            var result = new List<GetDataResponse>();

            foreach (var plate in raw?.PlateData ?? new())
            {
                foreach (var pair in plate.DailyCounts ?? new())
                {
                    result.Add(new GetDataResponse
                    {
                        ReportDate = pair.Key,
                        EntranceType = plate.EntranceType,
                        Transit = pair.Value?.Transit ?? 0,
                        IranianTruck = pair.Value?.IranianTruck ?? 0,
                        IranianPassenger = pair.Value?.IranianPassenger ?? 0
                    });
                }
            }

            return result;
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
