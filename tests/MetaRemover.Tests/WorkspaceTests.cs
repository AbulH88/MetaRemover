using MetaRemover.Core;
namespace MetaRemover.Tests;
public class WorkspaceTests
{
    [Fact]
    public async Task SelectedExportOnlyProcessesCheckedFilesAndPreservesOtherStatuses()
    {
        using var temp = new TempDirectory();
        var first = await ProcessingTests.CreateFixture(temp, ".jpg");
        var second = await ProcessingTests.CreateFixture(temp, ".png");
        var workspace = new WorkspaceModel(ProcessingTests.Paths);
        await workspace.ImportAsync([first, second], false, null);
        workspace.SelectNone();
        workspace.Files[1].IsChecked = true;
        var inspected = await workspace.InspectAsync(workspace.Files[0], default);
        Assert.False(workspace.Files[0].IsChecked);
        Assert.NotNull(inspected);
        await workspace.ExportAsync(true, new ExportSettings { RemoveOnly = true, OutputDirectory = System.IO.Path.Combine(temp.Path, "output") });
        Assert.Equal("Ready", workspace.Files[0].Status);
        Assert.Equal("Complete", workspace.Files[1].Status);
        Assert.Equal(1, workspace.Completed);
        Assert.False(workspace.IsBusy);
    }

    [Fact]
    public async Task AllExportIncludesUncheckedFilesAndContinuesAfterFailure()
    {
        using var temp = new TempDirectory();
        var bad = temp.File("bad.jpg");
        var good = await ProcessingTests.CreateFixture(temp, ".png");
        var workspace = new WorkspaceModel(ProcessingTests.Paths);
        await workspace.ImportAsync([bad, good], false, null);
        workspace.SelectNone();
        await workspace.ExportAsync(false, new ExportSettings { RemoveOnly = true, OutputDirectory = System.IO.Path.Combine(temp.Path, "output") });
        Assert.Equal("Failed", workspace.Files[0].Status);
        Assert.Equal("Complete", workspace.Files[1].Status);
        Assert.Equal(1, workspace.Completed);
        Assert.Equal(1, workspace.Failed);
    }
}
