using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PulsePilot.IntegrationTests.Api;

public sealed class PulsePilotApiFactory(
    string connectionString,
    Action<IServiceCollection>? configureTestServices = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration(configurationBuilder =>
        {
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = connectionString,
                ["Jwt:Issuer"] = "PulsePilot.IntegrationTests",
                ["Jwt:Audience"] = "PulsePilot.IntegrationTests.Client",
                ["Jwt:Secret"] = "integration-test-secret-at-least-32-bytes-long",
                ["Jwt:ExpirationMinutes"] = "60",
            };

            configurationBuilder.AddInMemoryCollection(values);
        });

        if (configureTestServices is not null)
        {
            builder.ConfigureTestServices(configureTestServices);
        }
    }
}
