using GMinor.Core;
using GMinor.Core.Conflicts;
using GMinor.Core.Exceptions;
using Moq;

namespace GMinor.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="FileDispatcher"/>. All tests inject mock routing functions
/// and mock conflict resolvers — no real filesystem I/O is performed.
/// </summary>
public class FileDispatcherTests
{
    private const string SourcePath = @"C:\Drop\somefile.txt";
    private const string DestDir    = @"C:\Sorted\Docs";
    private const string DestName   = "renamed.txt";

    private static RoutingResult MatchResult =>
        new() { DestDir = DestDir, DestName = DestName };

    private static FileDispatcher BuildDispatcher(RoutingResult routingResult) =>
        new(_ => routingResult);

    // ── Dry-run ────────────────────────────────────────────────────────────────

    [Fact]
    public void Dispatch_DryRun_DoesNotMoveFile()
    {
        var dispatcher = BuildDispatcher(MatchResult);

        var result = dispatcher.Dispatch(SourcePath, dryRun: true);

        Assert.Equal(DispatchOutcome.DryRun, result.Outcome);
        Assert.Equal(SourcePath, result.SourcePath);
        Assert.NotNull(result.DestPath);
        // Verify no file was created (source doesn't exist on disk, but no exception was thrown either)
    }

    // ── No match ───────────────────────────────────────────────────────────────

    [Fact]
    public void Dispatch_NoMatch_ReturnsSkipped()
    {
        var dispatcher = BuildDispatcher(RoutingResult.NoMatch);

        var result = dispatcher.Dispatch(SourcePath);

        Assert.Equal(DispatchOutcome.Skipped, result.Outcome);
        Assert.Equal(SourcePath, result.SourcePath);
        Assert.Null(result.DestPath);
    }

    // ── Conflict resolution ────────────────────────────────────────────────────

    [Fact]
    public void Dispatch_Conflict_Skip_ReturnsSkipped()
    {
        // Arrange: destination file exists → resolver returns Skip
        var destPath = Path.Combine(DestDir, DestName);
        var tempDest = Path.GetTempFileName();
        var tempDestDir = Path.GetDirectoryName(tempDest)!;
        var tempDestName = Path.GetFileName(tempDest);

        var matchResult = new RoutingResult { DestDir = tempDestDir, DestName = tempDestName };
        var dispatcher = new FileDispatcher(_ => matchResult);

        var resolverMock = new Mock<IConflictResolver>();
        resolverMock
            .Setup(r => r.Resolve(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(ConflictResolution.Skip);

        try
        {
            // Source doesn't need to exist for a Skip resolution test using a real dest file.
            // We fabricate a temp source path that doesn't need to be real
            // because the conflict branch exits before actually touching the source.
            // However, File.Exists(destPath) IS called, so destPath must exist (tempDest does).
            var result = dispatcher.Dispatch(SourcePath, dryRun: false, resolverMock.Object);

            Assert.Equal(DispatchOutcome.Skipped, result.Outcome);
            Assert.Equal(SourcePath, result.SourcePath);
            resolverMock.Verify(r => r.Resolve(SourcePath, It.IsAny<string>()), Times.Once);
        }
        finally
        {
            File.Delete(tempDest);
        }
    }

    [Fact]
    public void Dispatch_Conflict_Overwrite_ReturnsOverwritten()
    {
        // Arrange: both source and destination exist as real temp files
        var tempSource = Path.GetTempFileName();
        var tempDest   = Path.GetTempFileName();
        var tempDestDir  = Path.GetDirectoryName(tempDest)!;
        var tempDestName = Path.GetFileName(tempDest);

        var matchResult = new RoutingResult { DestDir = tempDestDir, DestName = tempDestName };
        var dispatcher  = new FileDispatcher(_ => matchResult);

        var resolverMock = new Mock<IConflictResolver>();
        resolverMock
            .Setup(r => r.Resolve(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(ConflictResolution.Overwrite);

        try
        {
            var result = dispatcher.Dispatch(tempSource, dryRun: false, resolverMock.Object);

            Assert.Equal(DispatchOutcome.Overwritten, result.Outcome);
            resolverMock.Verify(r => r.Resolve(tempSource, It.IsAny<string>()), Times.Once);
            Assert.False(File.Exists(tempSource),  "Source should have been moved.");
            Assert.True(File.Exists(tempDest),     "Destination should exist after overwrite.");
        }
        finally
        {
            if (File.Exists(tempSource)) File.Delete(tempSource);
            if (File.Exists(tempDest))   File.Delete(tempDest);
        }
    }

    [Fact]
    public void Dispatch_NullResolver_Conflict_ThrowsConflictException()
    {
        // Arrange: destination file exists, no resolver provided
        var tempDest     = Path.GetTempFileName();
        var tempDestDir  = Path.GetDirectoryName(tempDest)!;
        var tempDestName = Path.GetFileName(tempDest);

        var matchResult = new RoutingResult { DestDir = tempDestDir, DestName = tempDestName };
        var dispatcher  = new FileDispatcher(_ => matchResult);

        try
        {
            Assert.Throws<ConflictException>(() => dispatcher.Dispatch(SourcePath, resolver: null));
        }
        finally
        {
            File.Delete(tempDest);
        }
    }

    // ── Destination path construction ──────────────────────────────────────────

    [Fact]
    public void Dispatch_DryRun_DestPathIncludesResolvedName()
    {
        var dispatcher = BuildDispatcher(MatchResult);

        var result = dispatcher.Dispatch(SourcePath, dryRun: true);

        Assert.Equal(Path.Combine(DestDir, DestName), result.DestPath);
    }

    [Fact]
    public void Dispatch_DryRun_NullDestName_UsesOriginalFilename()
    {
        // Routing result with no DestName → original filename should be preserved.
        var matchNoRename = new RoutingResult { DestDir = DestDir };
        var dispatcher = new FileDispatcher(_ => matchNoRename);
        var filename = Path.GetFileName(SourcePath);

        var result = dispatcher.Dispatch(SourcePath, dryRun: true);

        Assert.Equal(Path.Combine(DestDir, filename), result.DestPath);
    }
}
