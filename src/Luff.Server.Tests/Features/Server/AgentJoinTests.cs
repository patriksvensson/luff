using Luff.Server.Features;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Server;

public sealed class AgentJoinTests
{
    [Fact]
    public void Should_Build_A_Curl_Pipe_Sh_One_Liner_With_All_Arguments()
    {
        // When
        var result = AgentJoin.BuildCommand(
            "patriksvensson/luff", "box2", "https://cp.example.ts.net:8443", "aB3+c/D9=", "luff_token01");

        // Then
        result.ShouldBe(
            "curl -fsSL https://github.com/patriksvensson/luff/releases/latest/download/agent-install.sh | sudo sh -s -- "
            + "--name 'box2' --server 'https://cp.example.ts.net:8443' --pin 'aB3+c/D9=' --token 'luff_token01'");
    }

    [Fact]
    public void Should_Escape_A_Single_Quote_In_A_Value()
    {
        // When
        var result = AgentJoin.BuildCommand("r/r", "o'brien", "s", "p", "t");

        // Then
        result.ShouldContain("--name 'o'\\''brien'");
    }
}
