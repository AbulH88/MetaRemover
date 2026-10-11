using MetaRemover.Core;
namespace MetaRemover.Tests;
public class RichCameraTests
{
    [Fact]
    public void CameraDefaultsAreModelSpecificAndDoNotInventUnavailableCameras()
    {
        var main=CameraCatalog.ForModel("iPhone 16 Pro").Single(c=>c.Name=="Main");
        Assert.Equal(1.78,main.Aperture); Assert.Equal(24,main.EquivalentMm);
        Assert.Equal(120,CameraCatalog.ForModel("iPhone 16 Pro").Single(c=>c.Name=="Telephoto").EquivalentMm);
        Assert.Equal(77,CameraCatalog.ForModel("iPhone 15 Pro").Single(c=>c.Name=="Telephoto").EquivalentMm);
        Assert.DoesNotContain(CameraCatalog.ForModel("iPhone 16"),c=>c.Name=="Telephoto");
        Assert.Empty(CameraCatalog.ForModel("Unknown phone"));
    }
    [Fact]
    public void RichSettingsRoundTripAndOldPresetsRemainLoadable()
    {
        using var temp=new TempDirectory();
        var path=System.IO.Path.Combine(temp.Path,"presets.json");
        var s=Settings with {OutputDirectory=""};
        var store=new SavedPresetStore(path);store.Save("Full camera",s);
        Assert.Equal(s,Assert.Single(new SavedPresetStore(path).Load()).Settings);
        System.IO.File.WriteAllText(path,"[{\"Name\":\"Old\",\"Settings\":{\"Model\":\"iPhone 15 Pro\"}}]");
        var old=Assert.Single(store.Load()).Settings;
        Assert.Null(old.FNumber);Assert.Equal("",old.LensModel);
    }
    [Theory]
    [InlineData(0)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void RejectsInvalidCameraNumbers(double value)
    {Assert.NotEmpty(new PresetService().Validate(Settings with {FNumber=value}));}
    [Fact]
    public void NumericExposureAndLensTagsAreWrittenOnlyToTheirSupportedFormats()
    {
        var service=new PresetService();var photo=service.BuildTags(MediaKind.Image,Settings);
        Assert.Equal("Apple",photo["EXIF:LensMake"]);
        Assert.Equal("1.78",photo["EXIF:FNumber"]);
        Assert.Equal("0.003125",photo["EXIF:ExposureTime"]);
        Assert.Equal("0",photo["EXIF:Flash#"]);
        var movie=service.BuildTags(MediaKind.Video,Settings);
        Assert.Equal(Settings.LensModel,movie["VideoKeys:LensModel"]);
        Assert.Equal("24",movie["VideoKeys:FocalLengthIn35mmFormat"]);
        Assert.DoesNotContain(movie.Keys,k=>k.Contains("ISO")||k.Contains("ExposureTime")||k.Contains("FNumber"));
        Assert.Empty(service.BuildTags(MediaKind.Image,Settings with {RemoveOnly=true}));
    }
    [Theory]
    [InlineData(".jpg")] [InlineData(".png")] [InlineData(".webp")] [InlineData(".mp4")] [InlineData(".mov")]
    public async Task RealExportWritesAndVerifiesRichTagsWithoutChangingOriginal(string extension)
    {
        using var temp=new TempDirectory();
        var path=System.IO.Path.Combine(temp.Path,"source"+extension);
        var video=extension is ".mp4" or ".mov";
        var args=new List<string>{"-v","error","-f","lavfi","-i","color=c=teal:s=160x96:r=5","-frames:v",video?"2":"1"};
        if(video)args.AddRange(["-c:v","mpeg4","-pix_fmt","yuv420p"]);
        args.AddRange(["-threads","1",path]);
        await new ToolRunner().RunAsync(ProcessingTests.Paths.Ffmpeg,args,default);
        var before=System.Security.Cryptography.SHA256.HashData(System.IO.File.ReadAllBytes(path));
        var s=Settings with {OutputDirectory=System.IO.Path.Combine(temp.Path,"out")};
        var result=await new ProcessingService(ProcessingTests.Paths).ExportAsync(new(path,video?MediaKind.Video:MediaKind.Image,1),s,new OutputAllocator(s.OutputDirectory,true,1),default);
        Assert.True(result.Success,result.Error);Assert.Empty(result.Warnings);
        var output=await new MetadataReader(ProcessingTests.Paths).ReadAsync(result.Path!,default);
        Assert.Equal(s.LensModel,output.Get("LensModel"));
        Assert.Equal("24",output.Get("FocalLengthIn35mmFormat"));
        if(!video){Assert.Equal("125",output.Get("ISO"));Assert.Equal("0.003125",output.Get("ExposureTime"));Assert.Equal("1.78",output.Get("FNumber"));}
        Assert.Equal(before,System.Security.Cryptography.SHA256.HashData(System.IO.File.ReadAllBytes(path)));
    }
    public static ExportSettings Settings=>new(){Camera="Main",LensMake="Apple",LensModel="iPhone 16 Pro main camera",FNumber=1.78,FocalLength=6.765,FocalLength35mm=24,Iso=125,ExposureSeconds=1d/320,ExposureBias=-0.3,Flash=0,WhiteBalance=0,Software="18.1",KeepOriginalDate=false,CaptureDate=DateTimeOffset.Parse("2025-02-03T12:34:56+06:00")};
}
