namespace IdentityManagementSystem.API.Modules.AccessControlReports.Common
{
    public class PomixResponse
    {
        public bool IsSuccessful { get; set; }
        public int Error { get; set; }
        public string ErrorDescription { get; set; }
        public string ResponseText { get; set; }
    }
}
