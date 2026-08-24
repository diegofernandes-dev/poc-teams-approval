using ApprovalGateway.Bot;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApprovalGateway.Tests;

public sealed class ApprovalGatewayAgentTests
{
    [Fact]
    public void PocReplyMessage_IsExpectedValue()
    {
        Assert.Equal("Approval Gateway POC is online.", ApprovalGatewayAgent.PocReplyMessage);
    }

    [Fact]
    public void Constructor_RegistersHandlersWithoutThrowing()
    {
        var options = new AgentApplicationOptions(new MemoryStorage());
        var agent = new ApprovalGatewayAgent(options, NullLogger<ApprovalGatewayAgent>.Instance);

        Assert.NotNull(agent);
    }
}
