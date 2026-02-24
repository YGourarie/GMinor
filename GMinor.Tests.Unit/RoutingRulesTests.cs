using GMinor.Core;
using GMinor.Core.Rules;

namespace GMinor.Tests.Unit;

public class RoutingRulesTests
{
    private const string DestRoot = @"C:\Sorted";

    [Fact]
    public void Route_PdfFile_RoutesToPdfSubfolder()
    {
        var result = RoutingRules.Route("report.pdf", DestRoot);

        Assert.True(result.IsMatch);
        Assert.Equal(Path.Combine(DestRoot, "pdf"), result.DestDir);
        Assert.Equal("moved-report.pdf", result.DestName);
    }

    [Fact]
    public void Route_IsCaseInsensitive_ExtensionLowercased()
    {
        var result = RoutingRules.Route("report.PDF", DestRoot);

        Assert.True(result.IsMatch);
        Assert.Equal(Path.Combine(DestRoot, "pdf"), result.DestDir);
        Assert.Equal("moved-report.PDF", result.DestName);
    }

    [Fact]
    public void Route_NoExtension_RoutesToMiscSubfolder()
    {
        var result = RoutingRules.Route("Makefile", DestRoot);

        Assert.True(result.IsMatch);
        Assert.Equal(Path.Combine(DestRoot, "misc"), result.DestDir);
        Assert.Equal("moved-Makefile", result.DestName);
    }

    [Fact]
    public void Route_EmptyFilename_ReturnsNoMatch()
    {
        var result = RoutingRules.Route("", DestRoot);

        Assert.False(result.IsMatch);
        Assert.Same(RoutingResult.NoMatch, result);
    }

    [Fact]
    public void Route_DestName_AlwaysHasMovedPrefix()
    {
        var result = RoutingRules.Route("document.docx", DestRoot);

        Assert.StartsWith("moved-", result.DestName);
    }

    [Theory]
    [InlineData("image.png",    "png")]
    [InlineData("sheet.xlsx",   "xlsx")]
    [InlineData("archive.zip",  "zip")]
    [InlineData("video.MP4",    "mp4")]
    public void Route_VariousExtensions_FolderMatchesExtension(string filename, string expectedFolder)
    {
        var result = RoutingRules.Route(filename, DestRoot);

        Assert.True(result.IsMatch);
        Assert.Equal(Path.Combine(DestRoot, expectedFolder), result.DestDir);
    }

    [Fact]
    public void Route_DestRootIsHonoured()
    {
        var customRoot = @"D:\MyFiles";
        var result = RoutingRules.Route("note.txt", customRoot);

        Assert.StartsWith(customRoot, result.DestDir);
    }
}
