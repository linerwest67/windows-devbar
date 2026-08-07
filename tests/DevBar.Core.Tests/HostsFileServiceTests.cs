using DevBar.Core.HostsFile;

namespace DevBar.Core.Tests;

public class HostsFileServiceTests
{
    [Fact]
    public void Parse_SkipsCommentsAndBlankLines()
    {
        string[] lines =
        [
            "# Copyright (c) 1993-2009 Microsoft Corp.",
            "",
            "#      102.54.94.97     rhino.acme.com          # source server",
            "127.0.0.1 localhost",
        ];

        var entries = HostsFileService.Parse(lines);

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e is { HostName: "rhino.acme.com", Enabled: false });
        Assert.Contains(entries, e => e is { HostName: "localhost", Enabled: true, IpAddress: "127.0.0.1" });
    }

    [Fact]
    public void Parse_ExpandsMultipleHostsOnOneLine()
    {
        var entries = HostsFileService.Parse(["127.0.0.1  app.local api.local  # dev"]);

        Assert.Equal(2, entries.Count);
        Assert.All(entries, e => Assert.Equal("127.0.0.1", e.IpAddress));
        Assert.All(entries, e => Assert.Equal("dev", e.Comment));
        Assert.Contains(entries, e => e.HostName == "app.local");
        Assert.Contains(entries, e => e.HostName == "api.local");
    }

    [Fact]
    public void Parse_IgnoresLinesWithoutValidIp()
    {
        var entries = HostsFileService.Parse(["not-an-ip somehost", "# just a comment", "::1 localhost"]);

        Assert.Single(entries);
        Assert.Equal("::1", entries[0].IpAddress);
    }

    [Theory]
    [InlineData("app.local", true)]
    [InlineData("my-api.dev_1", true)]
    [InlineData("localhost", true)]
    [InlineData("", false)]
    [InlineData("evil.com 0.0.0.0 hijacked.com", false)]  // whitespace injection
    [InlineData("host#comment", false)]                    // comment injection
    [InlineData("host\n0.0.0.0 injected.com", false)]      // newline injection
    [InlineData("-leading-dash", false)]
    public void IsValidHostName_RejectsFormatBreakingInput(string host, bool expected)
    {
        Assert.Equal(expected, HostsFileService.IsValidHostName(host));
    }

    [Fact]
    public void Render_StripsLineBreaksFromFields()
    {
        List<HostsEntry> entries = [new("127.0.0.1", "app.local", true, "line1\nline2")];

        var rendered = HostsFileService.Render(entries);
        var reparsed = HostsFileService.Parse(rendered.Split('\n'));

        Assert.Single(reparsed);
        Assert.Equal("app.local", reparsed[0].HostName);
    }

    [Fact]
    public void Render_RoundTripsThroughParse()
    {
        List<HostsEntry> original =
        [
            new("127.0.0.1", "app.local", true, "dev"),
            new("10.0.0.5", "staging.local", false, null),
        ];

        var reparsed = HostsFileService.Parse(HostsFileService.Render(original).Split('\n'));

        Assert.Equal(2, reparsed.Count);
        Assert.Equal(original[0].HostName, reparsed[0].HostName);
        Assert.True(reparsed[0].Enabled);
        Assert.Equal(original[1].HostName, reparsed[1].HostName);
        Assert.False(reparsed[1].Enabled);
    }
}
