using MetaRemover.Core;

namespace MetaRemover.Tests;

public class CoreTests
{
    [Fact]
    public void ImportDeduplicatesPathsButKeepsDifferentFilesAndExcludesOutput()
    {
        using var temp = new TempDirectory();
        var a = temp.File("a/photo.JPG");
        temp.File("b/photo.jpg");
        temp.File("clip.mp4");
        temp.File("output/export.jpg");
        temp.File("unsupported.txt");
        var result = new ImportService().Import([temp.Path, a.ToUpperInvariant()], true, System.IO.Path.Combine(temp.Path, "output"));
        Assert.Equal(3, result.Files.Count);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void NonrecursiveImportDoesNotDescendAndSupportsUnicode()
    {
        using var temp = new TempDirectory();
        temp.File("বাংলা photo.png");
        temp.File("nested/a.jpg");
        Assert.Single(new ImportService().Import([temp.Path], false, null).Files);
    }

    [Fact]
    public void NamingSkipsExistingFilesAndUsesActualFormat()
    {
        using var temp = new TempDirectory();
        temp.File("IMG_4827.JPG");
        var allocator = new OutputAllocator(temp.Path, true, 4827);
        using var first = allocator.Reserve("source.jpg");
        using var second = allocator.Reserve("source.mp4");
        Assert.Equal("IMG_4828.JPG", System.IO.Path.GetFileName(first.FinalPath));
        Assert.Equal("IMG_4829.MP4", System.IO.Path.GetFileName(second.FinalPath));
        Assert.Equal("", System.IO.File.ReadAllText(System.IO.Path.Combine(temp.Path, "IMG_4827.JPG")));
    }

    [Fact]
    public void NamingRollsOverAndReservationsPreventOverwrite()
    {
        using var temp = new TempDirectory();
        var allocator = new OutputAllocator(temp.Path, true, 9999);
        using var first = allocator.Reserve("source.mov");
        using var second = allocator.Reserve("source.jpg");
        Assert.Equal("IMG_9999.MOV", System.IO.Path.GetFileName(first.FinalPath));
        Assert.Equal("IMG_0001.JPG", System.IO.Path.GetFileName(second.FinalPath));
        Assert.NotEqual(System.IO.Path.GetDirectoryName(first.FinalPath), System.IO.Path.GetDirectoryName(second.FinalPath));
        System.IO.File.WriteAllText(first.StagingPath, "ready");
        first.Publish();
        Assert.Equal("ready", System.IO.File.ReadAllText(first.FinalPath));
    }

    [Fact]
    public void AbandonedReservationRemovesOnlyOwnedFiles()
    {
        using var temp = new TempDirectory();
        string final;
        string staging;
        using (var reservation = new OutputAllocator(temp.Path, true, 1).Reserve("a.webp"))
        {
            final = reservation.FinalPath; staging = reservation.StagingPath;
            System.IO.File.WriteAllText(staging, "partial");
        }
        Assert.False(System.IO.File.Exists(staging));
        Assert.False(System.IO.File.Exists(final));
    }

    [Fact]
    public void CanceledWriteRemovesExifToolSidecarAndPreservesOtherFiles()
    {
        using var temp = new TempDirectory();
        var unrelated = temp.File("other.jpg_exiftool_tmp");
        string sidecar;
        using (var output = new OutputAllocator(temp.Path, true, 1).Reserve("source.jpg"))
        {
            sidecar = output.StagingPath + "_exiftool_tmp";
            System.IO.File.WriteAllText(sidecar, "partial private bytes");
        }
        Assert.False(System.IO.File.Exists(sidecar));
        Assert.True(System.IO.File.Exists(unrelated));
    }

    [Fact]
    public void VideoPresetUsesAppleKeysAndExplicitOffset()
    {
        var settings = new ExportSettings { Model = "iPhone 16 Pro", CaptureDate = DateTimeOffset.Parse("2026-10-11T19:30:00+06:00") };
        var tags = new PresetService().BuildTags(MediaKind.Video, settings);
        Assert.Equal("Apple", tags["Keys:Make"]);
        Assert.Equal("iPhone 16 Pro", tags["Keys:Model"]);
        Assert.Equal("2026:10:11 19:30:00+06:00", tags["Keys:CreationDate"]);
        Assert.DoesNotContain(tags.Keys, k => k.Contains("GPS"));
    }

    [Fact]
    public void InvalidCoordinatesAndIncompleteGpsAreRejected()
    {
        var service = new PresetService();
        Assert.NotEmpty(service.Validate(new ExportSettings { Latitude = 91, Longitude = 0 }));
        Assert.NotEmpty(service.Validate(new ExportSettings { Latitude = 30 }));
        Assert.Empty(service.BuildTags(MediaKind.Image, new ExportSettings { RemoveOnly = true }));
    }
}

public sealed class TempDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MetaRemoverTests", Guid.NewGuid().ToString("N"));
    public TempDirectory() => Directory.CreateDirectory(Path);
    public string File(string name)
    {
        var path = System.IO.Path.Combine(Path, name);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, "");
        return path;
    }
    public void Dispose() => Directory.Delete(Path, true);
}
