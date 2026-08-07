using DevBar.Core.PackageManagers;

namespace DevBar.Core.Tests;

public class WingetServiceTests
{
    [Fact]
    public void ParseUpgradeTable_ReadsFixedWidthColumns()
    {
        const string output = """
            Name                 Id                      Version      Available    Source
            -------------------------------------------------------------------------------
            Git                  Git.Git                 2.43.0       2.44.0       winget
            Node.js              OpenJS.NodeJS.LTS       20.11.0      20.12.2      winget

            2 upgrades available.
            """;

        var upgrades = WingetService.ParseUpgradeTable(output);

        Assert.Equal(2, upgrades.Count);
        Assert.Equal(new WingetUpgrade("Git", "Git.Git", "2.43.0", "2.44.0"), upgrades[0]);
        Assert.Equal("OpenJS.NodeJS.LTS", upgrades[1].Id);
        Assert.Equal("20.12.2", upgrades[1].AvailableVersion);
    }

    [Fact]
    public void ParseUpgradeTable_ReturnsEmptyWithoutHeader()
    {
        Assert.Empty(WingetService.ParseUpgradeTable("No installed package found matching input criteria."));
    }

    [Fact]
    public void ParseUpgradeTable_StopsAtPinnedPackagesSection()
    {
        const string output = """
            Name       Id            Version   Available   Source
            ------------------------------------------------------
            Git        Git.Git       2.43.0    2.44.0      winget

            The following packages have an upgrade available, but require explicit targeting:
            Foo        Foo.Bar       1.0       2.0         winget
            """;

        var upgrades = WingetService.ParseUpgradeTable(output);

        Assert.Single(upgrades);
        Assert.Equal("Git", upgrades[0].Name);
    }
}
