using System.Diagnostics;
using System.Security.Cryptography;
using MetaRemover.Core;

namespace MetaRemover.Tests;

public class ProcessingTests
{
    [Fact]
    public async Task MultilineExifArgumentsAreRejectedRatherThanInterpretedAsOptions()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => new ToolRunner().RunAsync(Paths.ExifTool, ["-ver\n-execute"], default));
    }

    [Fact]
    public async Task DisguisedFileFormatIsRejectedWithoutPublishing()
    {
        using var temp = new TempDirectory();
        var png = await CreateFixture(temp, ".png");
        var fakeJpeg = System.IO.Path.Combine(temp.Path, "disguised.jpg");
        File.Copy(png, fakeJpeg);
        var settings = new ExportSettings { RemoveOnly = true, OutputDirectory = System.IO.Path.Combine(temp.Path, "out") };
        var result = await new ProcessingService(Paths).ExportAsync(new(fakeJpeg, MediaKind.Image, 1), settings,
            new OutputAllocator(settings.OutputDirectory, true, 1), default);
        Assert.False(result.Success);
        Assert.Empty(Directory.GetFiles(settings.OutputDirectory));
    }

    [Theory]
    [InlineData(".jpg")] [InlineData(".png")] [InlineData(".webp")]
    public async Task ImageColorProfileBytesArePreserved(string extension)
    {
        using var temp = new TempDirectory();
        var source = await CreateFixture(temp, extension);
        var profile = System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "srgb.icc");
        await Run(Paths.ExifTool, ["-overwrite_original", "-ICC_Profile<=" + profile, source]);
        var before = await Run(Paths.ExifTool, ["-json", "-b", "-ICC_Profile", source]);
        var settings = new ExportSettings { RemoveOnly = true, OutputDirectory = System.IO.Path.Combine(temp.Path, "out") };
        var result = await new ProcessingService(Paths).ExportAsync(new(source, MediaKind.Image, 1), settings,
            new OutputAllocator(settings.OutputDirectory, true, 1), default);
        Assert.True(result.Success, result.Error);
        var after = await Run(Paths.ExifTool, ["-json", "-b", "-ICC_Profile", result.Path!]);
        using var a = System.Text.Json.JsonDocument.Parse(before);
        using var b = System.Text.Json.JsonDocument.Parse(after);
        Assert.StartsWith("base64:", a.RootElement[0].GetProperty("ICC_Profile").GetString());
        Assert.Equal(a.RootElement[0].GetProperty("ICC_Profile").GetString(), b.RootElement[0].GetProperty("ICC_Profile").GetString());
    }

    [Fact]
    public async Task HdrVideoRetainsTenBitCodecAndColorSignaling()
    {
        using var temp = new TempDirectory();
        var source = System.IO.Path.Combine(temp.Path, "hdr.mp4");
        await Run(Paths.Ffmpeg, ["-v", "error", "-f", "lavfi", "-i", "color=c=teal:s=160x96:r=5", "-frames:v", "2", "-c:v", "libsvtav1", "-preset", "12", "-svtav1-params", "lp=2:color-primaries=9:transfer-characteristics=16:matrix-coefficients=9", "-pix_fmt", "yuv420p10le", "-color_primaries", "bt2020", "-color_trc", "smpte2084", "-colorspace", "bt2020nc", source]);
        var before = await new MetadataReader(Paths).ReadAsync(source, default);
        Assert.Equal("smpte2084", before.Streams[0].ColorTransfer);
        var settings = new ExportSettings { RemoveOnly = true, OutputDirectory = System.IO.Path.Combine(temp.Path, "out") };
        var result = await new ProcessingService(Paths).ExportAsync(new(source, MediaKind.Video, 1), settings,
            new OutputAllocator(settings.OutputDirectory, true, 1), default);
        Assert.True(result.Success, result.Error);
        Assert.Equal(before.Streams[0], (await new MetadataReader(Paths).ReadAsync(result.Path!, default)).Streams[0]);
        Assert.Equal(await MediaHash(source, ".mp4"), await MediaHash(result.Path!, ".mp4"));
    }

    [Theory]
    [InlineData(".mp4")] [InlineData(".mov")]
    public async Task VideoIntegerDatesStoreUtcWithoutDoubleConversion(string extension)
    {
        using var temp = new TempDirectory();
        var source = await CreateFixture(temp, extension);
        var settings = new ExportSettings { KeepOriginalDate = false, CaptureDate = DateTimeOffset.Parse("2026-10-11T19:30:00+06:00"), OutputDirectory = System.IO.Path.Combine(temp.Path, "out") };
        var result = await new ProcessingService(Paths).ExportAsync(new(source, MediaKind.Video, 1), settings,
            new OutputAllocator(settings.OutputDirectory, true, 1), default);
        Assert.True(result.Success, result.Error);
        var raw = await Run(Paths.ExifTool, ["-api", "QuickTimeUTC=0", "-s", "-s", "-s", "-QuickTime:CreateDate", result.Path!]);
        Assert.Equal("2026:10:11 13:30:00", raw.Trim());
    }

    public static string Root
    {
        get
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !Directory.Exists(System.IO.Path.Combine(d.FullName, "tools"))) d = d.Parent;
            return d?.FullName ?? throw new Exception("Project tools not found");
        }
    }
    public static ToolPaths Paths => new(
        System.IO.Path.Combine(Root, "tools/exiftool/ExifTool.exe"),
        System.IO.Path.Combine(Root, "tools/ffmpeg/ffmpeg.exe"),
        System.IO.Path.Combine(Root, "tools/ffmpeg/ffprobe.exe"));

    [Theory]
    [InlineData(".jpg")] [InlineData(".png")] [InlineData(".webp")]
    [InlineData(".mp4")] [InlineData(".mov")]
    public async Task RemovalPreservesSourceAndRemovesGpsAndCamera(string extension)
    {
        using var temp = new TempDirectory();
        var source = await CreateFixture(temp, extension);
        var beforeHash = SHA256.HashData(await File.ReadAllBytesAsync(source));
        var processor = new ProcessingService(Paths);
        var reader = new MetadataReader(Paths);
        var original = await reader.ReadAsync(source, default);
        Assert.Contains(original.Tags.Keys, k => k.Contains("GPS"));
        var settings = new ExportSettings { RemoveOnly = true, OutputDirectory = System.IO.Path.Combine(temp.Path, "output") };
        var result = await processor.ExportAsync(new(source, extension is ".mov" or ".mp4" ? MediaKind.Video : MediaKind.Image, 1), settings,
            new OutputAllocator(settings.OutputDirectory, true, 1), default);
        Assert.True(result.Success, result.Error);
        Assert.True(result.Warnings.Count == 0, string.Join(" | ", result.Warnings));
        Assert.Equal(beforeHash, SHA256.HashData(await File.ReadAllBytesAsync(source)));
        var after = await reader.ReadAsync(result.Path!, default);
        Assert.DoesNotContain(after.Tags.Keys, k => k.Contains("GPS") || k.EndsWith(":Model") || k.EndsWith(":Artist"));
        Assert.Equal(original.Width, after.Width);
        Assert.Equal(original.Height, after.Height);
        Assert.Equal(await MediaHash(source, extension), await MediaHash(result.Path!, extension));
    }

    [Theory]
    [InlineData(".jpg")] [InlineData(".png")] [InlineData(".webp")]
    [InlineData(".mp4")] [InlineData(".mov")]
    public async Task ReplacementWritesVerifiedModelDateAndGps(string extension)
    {
        using var temp = new TempDirectory();
        var source = await CreateFixture(temp, extension);
        var settings = new ExportSettings { Model = "iPhone 16 Pro", KeepOriginalDate = false, CaptureDate = DateTimeOffset.Parse("2026-10-11T19:30:00+06:00"),
            Latitude = 23.8103, Longitude = 90.4125, OutputDirectory = System.IO.Path.Combine(temp.Path, "out") };
        var result = await new ProcessingService(Paths).ExportAsync(new(source, extension is ".mp4" or ".mov" ? MediaKind.Video : MediaKind.Image, 1), settings,
            new OutputAllocator(settings.OutputDirectory, true, 1), default);
        Assert.True(result.Success, result.Error);
        var tags = (await new MetadataReader(Paths).ReadAsync(result.Path!, default)).Tags;
        Assert.Contains(tags, kv => kv.Key.EndsWith(":Model") && kv.Value == "iPhone 16 Pro");
        Assert.Contains(tags, kv => kv.Key.Contains("GPS"));
        Assert.Contains(tags, kv => kv.Key.EndsWith(":DateTimeOriginal") && kv.Value == "2026:10:11 19:30:00"
            || kv.Key.EndsWith(":CreationDate") && kv.Value == "2026:10:11 19:30:00+06:00");
        Assert.Equal(await MediaHash(source, extension), await MediaHash(result.Path!, extension));
    }

    [Fact]
    public async Task CancellationDoesNotPublishOutputOrAlterSource()
    {
        using var temp = new TempDirectory();
        var source = await CreateFixture(temp, ".mp4");
        var settings = new ExportSettings { OutputDirectory = System.IO.Path.Combine(temp.Path, "out") };
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ProcessingService(Paths).ExportAsync(new(source, MediaKind.Video, 1),
            settings, new OutputAllocator(settings.OutputDirectory, true, 1), cts.Token));
        Assert.Empty(Directory.GetFiles(settings.OutputDirectory));
        Assert.True(File.Exists(source));
    }

    [Fact]
    public async Task CorruptFileFailsWithoutPublishingAnything()
    {
        using var temp = new TempDirectory();
        var source = temp.File("bad.jpg");
        var settings = new ExportSettings { RemoveOnly = true, OutputDirectory = System.IO.Path.Combine(temp.Path, "out") };
        var result = await new ProcessingService(Paths).ExportAsync(new(source, MediaKind.Image, 0), settings,
            new OutputAllocator(settings.OutputDirectory, true, 1), default);
        Assert.False(result.Success);
        Assert.Empty(Directory.GetFiles(settings.OutputDirectory));
    }

    [Fact]
    public async Task RotatedImageRetainsOrientation()
    {
        using var temp = new TempDirectory();
        var source = await CreateFixture(temp, ".jpg");
        await Run(Paths.ExifTool, ["-overwrite_original", "-Orientation#=6", source]);
        var settings = new ExportSettings { RemoveOnly = true, OutputDirectory = System.IO.Path.Combine(temp.Path, "out") };
        var result = await new ProcessingService(Paths).ExportAsync(new(source, MediaKind.Image, 1), settings,
            new OutputAllocator(settings.OutputDirectory, true, 1), default);
        Assert.True(result.Success, result.Error);
        Assert.Equal("6", (await new MetadataReader(Paths).ReadAsync(result.Path!, default)).Tags["IFD0:Orientation"]);
    }

    public static async Task<string> CreateFixture(TempDirectory temp, string extension, bool audio = true)
    {
        var source = System.IO.Path.Combine(temp.Path, "বাংলা & camera" + extension);
        var video = extension is ".mp4" or ".mov";
        List<string> args = ["-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "color=c=teal:s=160x96:r=5"];
        if (video)
        {
            if (audio) args.AddRange(["-f", "lavfi", "-i", "sine=frequency=400:sample_rate=44100"]);
            args.AddRange(["-t", "0.6", "-c:v", "mpeg4"]);
            if (audio) args.AddRange(["-c:a", "aac", "-shortest"]);
        }
        else args.AddRange(["-frames:v", "1", "-threads", "1", "-update", "1"]);
        args.AddRange(["-y", source]);
        await Run(Paths.Ffmpeg, args);
        await Run(Paths.ExifTool, video
            ? ["-overwrite_original", "-Keys:Make=Canon", "-Keys:Model=Old Camera", "-Keys:GPSCoordinates=23.8 90.4", "-Keys:CreationDate=2020:01:02 03:04:05+06:00", source]
            : ["-overwrite_original", "-EXIF:Make=Canon", "-EXIF:Model=Old Camera", "-GPSLatitude=23.8", "-GPSLatitudeRef=N", "-GPSLongitude=90.4", "-GPSLongitudeRef=E", "-DateTimeOriginal=2020:01:02 03:04:05", "-Artist=Private name", source]);
        return source;
    }

    [Theory]
    [InlineData(".mov")] [InlineData(".mp4")]
    public async Task SilentVideosPreserveStreamsAndOriginalCaptureDate(string extension)
    {
        using var temp = new TempDirectory();
        var source = await CreateFixture(temp, extension, audio: false);
        var settings = new ExportSettings { OutputDirectory = System.IO.Path.Combine(temp.Path, "out"), KeepOriginalDate = true };
        var result = await new ProcessingService(Paths).ExportAsync(new(source, MediaKind.Video, 1), settings,
            new OutputAllocator(settings.OutputDirectory, true, 1), default);
        Assert.True(result.Success, result.Error);
        var after = await new MetadataReader(Paths).ReadAsync(result.Path!, default);
        Assert.Single(after.Streams);
        Assert.Equal("2020:01:02 03:04:05+06:00", after.Get("CreationDate"));
        Assert.Equal(await MediaHash(source, extension), await MediaHash(result.Path!, extension));
    }

    [Fact]
    public async Task ExtraTracksRequireExplicitOptInAndAreReported()
    {
        using var temp = new TempDirectory();
        var original = await CreateFixture(temp, ".mp4");
        var subtitles = System.IO.Path.Combine(temp.Path, "captions.srt");
        await File.WriteAllTextAsync(subtitles, "1\n00:00:00,000 --> 00:00:00,500\nPrivate caption\n");
        var source = System.IO.Path.Combine(temp.Path, "with captions.mp4");
        await Run(Paths.Ffmpeg, ["-v", "error", "-i", original, "-i", subtitles, "-map", "0", "-map", "1", "-c", "copy", "-c:s", "mov_text", source]);
        var settings = new ExportSettings { RemoveOnly = true, OutputDirectory = System.IO.Path.Combine(temp.Path, "out") };
        var processor = new ProcessingService(Paths);
        var result = await processor.ExportAsync(new(source, MediaKind.Video, 1), settings,
            new OutputAllocator(settings.OutputDirectory, true, 1), default);
        Assert.False(result.Success);
        Assert.Empty(Directory.GetFiles(settings.OutputDirectory));
        var allowed = await processor.ExportAsync(new(source, MediaKind.Video, 1), settings with { AllowExtraTrackRemoval = true },
            new OutputAllocator(settings.OutputDirectory, true, 1), default);
        Assert.True(allowed.Success, allowed.Error);
        Assert.Contains(allowed.Warnings, w => w.Contains("tracks were excluded"));
        Assert.False((await new MetadataReader(Paths).ReadAsync(allowed.Path!, default)).HasExtraTracks);
    }

    [Fact]
    public async Task RotatedVideoRetainsDisplayRotation()
    {
        using var temp = new TempDirectory();
        var original = await CreateFixture(temp, ".mp4");
        var source = System.IO.Path.Combine(temp.Path, "rotated.mp4");
        await Run(Paths.Ffmpeg, ["-v", "error", "-display_rotation:v:0", "90", "-i", original, "-map", "0", "-c", "copy", source]);
        var before = await new MetadataReader(Paths).ReadAsync(source, default);
        Assert.Equal(90, Math.Abs(before.Streams.First(s => s.Type == "video").Rotation));
        var settings = new ExportSettings { RemoveOnly = true, OutputDirectory = System.IO.Path.Combine(temp.Path, "out") };
        var result = await new ProcessingService(Paths).ExportAsync(new(source, MediaKind.Video, 1), settings,
            new OutputAllocator(settings.OutputDirectory, true, 1), default);
        Assert.True(result.Success, result.Error);
        Assert.Equal(before.Streams[0].Rotation, (await new MetadataReader(Paths).ReadAsync(result.Path!, default)).Streams[0].Rotation);
    }

    [Theory]
    [InlineData(".jpg")] [InlineData(".mov")]
    public async Task NegativeGpsCoordinatesRetainHemisphere(string extension)
    {
        using var temp = new TempDirectory();
        var source = await CreateFixture(temp, extension);
        var settings = new ExportSettings { Latitude = -33.86, Longitude = -151.2, OutputDirectory = System.IO.Path.Combine(temp.Path, "out") };
        var result = await new ProcessingService(Paths).ExportAsync(new(source, extension == ".jpg" ? MediaKind.Image : MediaKind.Video, 1), settings,
            new OutputAllocator(settings.OutputDirectory, true, 1), default);
        Assert.True(result.Success, result.Error);
        var after = await new MetadataReader(Paths).ReadAsync(result.Path!, default);
        Assert.Equal("-33.86", after.Tags["Composite:GPSLatitude"]);
        Assert.Equal("-151.2", after.Tags["Composite:GPSLongitude"]);
    }

    public static async Task<string> MediaHash(string path, string extension)
    {
        var args = new List<string> { "-v", "error", "-i", path, "-map", "0:v", "-map", "0:a?" };
        if (extension is ".mov" or ".mp4") args.AddRange(["-c", "copy"]);
        else args.AddRange(["-frames:v", "1", "-pix_fmt", "rgb24"]);
        args.AddRange(["-f", "streamhash", "-hash", "sha256", "-"]);
        return (await Run(Paths.Ffmpeg, args)).Trim();
    }

    public static async Task<string> Run(string exe, IReadOnlyList<string> args)
    {
        string? argumentFile = null;
        if (System.IO.Path.GetFileName(exe).Equals("ExifTool.exe", StringComparison.OrdinalIgnoreCase))
        {
            argumentFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "exif-args-" + Guid.NewGuid().ToString("N") + ".txt");
            await File.WriteAllLinesAsync(argumentFile, args, new System.Text.UTF8Encoding(false));
            args = ["-charset", "filename=UTF8", "-@", argumentFile];
        }
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        psi.Environment.Remove("LC_ALL"); psi.Environment.Remove("LC_CTYPE"); psi.Environment.Remove("LANG");
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await stdout; var error = await stderr;
        if (argumentFile != null) File.Delete(argumentFile);
        if (process.ExitCode != 0) throw new Exception(error + output);
        return output;
    }
}
