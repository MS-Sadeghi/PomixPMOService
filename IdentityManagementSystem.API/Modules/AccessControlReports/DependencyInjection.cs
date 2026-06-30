using IdentityManagementSystem.API.Modules.AccessControlReports.Common;
using IdentityManagementSystem.API.Modules.AccessControlReports.GetData;
using Microsoft.Extensions.Options;

namespace IdentityManagementSystem.API.Modules.AccessControlReports;

public static class DependencyInjection
{
    public static IServiceCollection AddAccessControlReports(
        this IServiceCollection services)
    {
        services.AddHttpClient<IPomixClient, PomixClient>((sp, client) =>
        {
            var options = sp
                .GetRequiredService<IOptions<PomixOptions>>()
                .Value;

            client.BaseAddress = new Uri(options.BaseUrl);
        });

        services.AddScoped<GetDataHandler>();

        // در آینده
        // services.AddScoped<GetSumHandler>();
        // services.AddScoped<TrafficByTypeHandler>();
        // services.AddScoped<TrafficByPlatesHandler>();
        // services.AddScoped<TrafficByNationalIdHandler>();

        return services;
    }
}