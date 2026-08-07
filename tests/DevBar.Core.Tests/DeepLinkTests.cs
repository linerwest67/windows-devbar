using DevBar.Core.Ipc;

namespace DevBar.Core.Tests;

public class DeepLinkTests
{
    [Theory]
    [InlineData("devbar://open/ports", "OPEN-TAB ports")]
    [InlineData("devbar://open/docker", "OPEN-TAB docker")]
    [InlineData("devbar://open", "OPEN-TAB vitals")]
    [InlineData("devbar://kill/3000", "KILL-PORT-ASK 3000")] // deep-link kills must require confirmation
    public void ToPipeRequest_MapsSupportedUris(string uri, string expected)
    {
        Assert.Equal(expected, DeepLink.ToPipeRequest(uri));
    }

    [Theory]
    [InlineData("https://example.com/open/ports")]  // wrong scheme
    [InlineData("devbar://kill/notaport")]
    [InlineData("devbar://bogus/thing")]
    [InlineData("not a uri at all")]
    public void ToPipeRequest_RejectsUnsupportedInput(string uri)
    {
        Assert.Null(DeepLink.ToPipeRequest(uri));
    }

    [Fact]
    public void ToPipeRequest_RejectsOutOfRangePort()
    {
        Assert.Null(DeepLink.ToPipeRequest("devbar://kill/99999"));
    }
}
