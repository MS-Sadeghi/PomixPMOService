namespace IdentityManagementSystem.API.Modules.AccessControlReports.Common
{
    public class PomixOptions
    {
        public string BaseUrl { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }

    public class PomixResult
    {
        public bool IsSuccessful { get; set; }

        public int ResponseStatusCode { get; set; }

        public string ResponseText { get; set; } = string.Empty;
    }
}