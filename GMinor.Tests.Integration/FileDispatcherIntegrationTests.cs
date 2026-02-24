using GMinor.Core;
using GMinor.Core.Conflicts;
using GMinor.Core.Exceptions;
using GMinor.Core.Rules;
using Moq;

namespace GMinor.Tests.Integration;

/// <summary>
/// Integration tests that exercise <see cref="FileDispatcher"/> against a real temporary
/// filesystem. Each test class instance creates an isolated temp directory and cleans it up
/// via <see cref="IDisposable"/>.
/// </summary>
public class FileDispatcherIntegrationTests : IDisposable
{
    private readonly string _tempRoot;

    public FileDispatcherIntegrationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"GMinorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string CreateFile(string filename, string content = "test")
    {
        var path = Path.Combine(_tempRoot, filename);
        File.WriteAllText(path, content);
        return path;
    }

    private static FileDispatcher BuildDispatcher(string destDir, string? destName = null) =>
        new(_ => new RoutingResult { DestDir = destDir, DestName = destName });

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Dispatch_MovesFile_ToCorrectFolder()
    {
        var destDir  = Path.Combine(_tempRoot, "Dest");
        var source   = CreateFile("file.txt");
        var dispatcher = BuildDispatcher(destDir, "file.txt");

        var result = dispatcher.Dispatch(source);

        Assert.Equal(DispatchOutcome.Moved, result.Outcome);
        Assert.False(File.Exists(source));
        Assert.True(File.Exists(Path.Combine(destDir, "file.txt")));
    }

    [Fact]
    public void Dispatch_RenamesFile_AtDestination()
    {
        var destDir  = Path.Combine(_tempRoot, "Dest");
        var source   = CreateFile("original.txt");
        var dispatcher = BuildDispatcher(destDir, "renamed.txt");

        var result = dispatcher.Dispatch(source);

        Assert.Equal(DispatchOutcome.Moved, result.Outcome);
        Assert.False(File.Exists(source));
        Assert.True(File.Exists(Path.Combine(destDir, "renamed.txt")));
    }

    [Fact]
    public void Dispatch_CreatesDestination_IfMissing()
    {
        var destDir = Path.Combine(_tempRoot, "NonExistent", "SubDir");
        var source  = CreateFile("file.txt");
        var dispatcher = BuildDispatcher(destDir, "file.txt");

        dispatcher.Dispatch(source);

        Assert.True(Directory.Exists(destDir));
        Assert.True(File.Exists(Path.Combine(destDir, "file.txt")));
    }

    [Fact]
    public void Dispatch_NoMatch_LeavesFileInPlace()
    {
        var source     = CreateFile("unrecognized.xyz");
        var dispatcher = new FileDispatcher(_ => RoutingResult.NoMatch);

        var result = dispatcher.Dispatch(source);

        Assert.Equal(DispatchOutcome.Skipped, result.Outcome);
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void Dispatch_Conflict_Skip_LeavesSourceIntact()
    {
        var destDir  = Path.Combine(_tempRoot, "Dest");
        Directory.CreateDirectory(destDir);
        var destFile = Path.Combine(destDir, "file.txt");
        File.WriteAllText(destFile, "existing");

        var source     = CreateFile("file.txt", "new");
        var dispatcher = BuildDispatcher(destDir, "file.txt");

        var resolverMock = new Mock<IConflictResolver>();
        resolverMock.Setup(r => r.Resolve(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(ConflictResolution.Skip);

        var result = dispatcher.Dispatch(source, resolver: resolverMock.Object);

        Assert.Equal(DispatchOutcome.Skipped, result.Outcome);
        Assert.True(File.Exists(source), "Source must remain untouched after Skip.");
        Assert.Equal("existing", File.ReadAllText(destFile));
    }

    [Fact]
    public void Dispatch_Conflict_Overwrite_ReplacesDestination()
    {
        var destDir  = Path.Combine(_tempRoot, "Dest");
        Directory.CreateDirectory(destDir);
        var destFile = Path.Combine(destDir, "file.txt");
        File.WriteAllText(destFile, "existing");

        var source     = CreateFile("file.txt", "new-content");
        var dispatcher = BuildDispatcher(destDir, "file.txt");

        var resolverMock = new Mock<IConflictResolver>();
        resolverMock.Setup(r => r.Resolve(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(ConflictResolution.Overwrite);

        var result = dispatcher.Dispatch(source, resolver: resolverMock.Object);

        Assert.Equal(DispatchOutcome.Overwritten, result.Outcome);
        Assert.False(File.Exists(source));
        Assert.Equal("new-content", File.ReadAllText(destFile));
    }

    [Fact]
    public void Dispatch_LockedFile_ThrowsFileLockedException()
    {
        if (!OperatingSystem.IsWindows())
        {
            // On Linux, file locking is advisory: File.Move succeeds even when a
            // FileStream holds an exclusive lock. This behavior is Windows-specific.
            return;
        }

        var destDir = Path.Combine(_tempRoot, "Dest");
        var source  = CreateFile("locked.txt");

        // Hold an exclusive lock on the source file
        using var stream = new FileStream(source, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var dispatcher = BuildDispatcher(destDir, "locked.txt");

        Assert.Throws<FileLockedException>(() => dispatcher.Dispatch(source));
    }

    [Fact]
    public void EndToEnd_MultipleFiles_AllRoutedCorrectly()
    {
        var destBase   = Path.Combine(_tempRoot, "Sorted");
        var dispatcher = new FileDispatcher(f => RoutingRules.Route(f, destBase));

        var pdfFile = CreateFile("report.pdf");
        var txtFile = CreateFile("notes.txt");
        var noExt   = CreateFile("Makefile");

        var r1 = dispatcher.Dispatch(pdfFile);
        var r2 = dispatcher.Dispatch(txtFile);
        var r3 = dispatcher.Dispatch(noExt);

        Assert.Equal(DispatchOutcome.Moved, r1.Outcome);
        Assert.Equal(DispatchOutcome.Moved, r2.Outcome);
        Assert.Equal(DispatchOutcome.Moved, r3.Outcome);

        Assert.True(File.Exists(Path.Combine(destBase, "pdf",  "moved-report.pdf")));
        Assert.True(File.Exists(Path.Combine(destBase, "txt",  "moved-notes.txt")));
        Assert.True(File.Exists(Path.Combine(destBase, "misc", "moved-Makefile")));
    }

    [Fact]
    public void Dispatch_DryRun_DoesNotMoveFile()
    {
        var destDir = Path.Combine(_tempRoot, "Dest");
        var source  = CreateFile("file.txt");
        var dispatcher = BuildDispatcher(destDir, "file.txt");

        var result = dispatcher.Dispatch(source, dryRun: true);

        Assert.Equal(DispatchOutcome.DryRun, result.Outcome);
        Assert.True(File.Exists(source), "Source must remain in place during dry-run.");
        Assert.False(Directory.Exists(destDir), "Destination dir must not be created during dry-run.");
    }
}
