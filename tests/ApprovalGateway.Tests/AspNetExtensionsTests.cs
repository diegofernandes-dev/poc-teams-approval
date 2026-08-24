using ApprovalGateway.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApprovalGateway.Tests;

public sealed class AspNetExtensionsTests
{
    [Fact]
    public void AddAgentAspNetAuthentication_ThrowsWhenAudiencesMissing()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TokenValidation:TenantId"] = "e9dbba09-e7a3-42be-9a2c-f82470024e00",
            })
            .Build();

        var exception = Assert.Throws<ArgumentException>(() =>
            services.AddAgentAspNetAuthentication(configuration));

        Assert.Contains("Audiences", exception.Message, StringComparison.Ordinal);
    }
}
