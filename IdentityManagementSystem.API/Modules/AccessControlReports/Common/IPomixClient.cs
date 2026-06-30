namespace IdentityManagementSystem.API.Modules.AccessControlReports.Common
{
    public interface IPomixClient
    {
        Task<T> ExecuteAsync<T>(
            string serviceName,
            object[] parameters);
    }
}
