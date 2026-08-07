using DevBar.Core.Wsl;

namespace DevBar.Core.Tests;

public class WslServiceTests
{
    [Fact]
    public void ParseListOutput_ReadsNameStateAndVersion()
    {
        const string output = """
              NAME              STATE           VERSION
            * Ubuntu            Running         2
              docker-desktop    Stopped         2
            """;

        var distros = WslService.ParseListOutput(output);

        Assert.Equal(2, distros.Count);
        Assert.Equal(new WslDistro("Ubuntu", "Running", 2, true), distros[0]);
        Assert.Equal(new WslDistro("docker-desktop", "Stopped", 2, false), distros[1]);
    }

    [Fact]
    public void ParseListOutput_HandlesNamesWithSpaces()
    {
        const string output = """
              NAME                   STATE           VERSION
            * My Custom Distro       Running         2
            """;

        var distros = WslService.ParseListOutput(output);

        Assert.Single(distros);
        Assert.Equal("My Custom Distro", distros[0].Name);
        Assert.True(distros[0].IsDefault);
    }

    [Fact]
    public void ParseListOutput_ReturnsEmptyWhenNoDistros()
    {
        Assert.Empty(WslService.ParseListOutput("  NAME  STATE  VERSION\n"));
    }
}
