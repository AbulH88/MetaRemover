using MetaRemover.Core;
namespace MetaRemover.Tests;
public class AutoPresetTests
{
    [Theory]
    [InlineData("iPhone 16")] [InlineData("iPhone 16e")] [InlineData("iPhone 16 Pro")]
    public void PhoneSelectionProvidesCompleteEditableExampleSettings(string model)
    {
        var s=AutoPresetService.Create(model,"Telephoto");
        Assert.Equal(model,s.Model);Assert.NotEmpty(s.LensModel);Assert.Equal("Apple",s.LensMake);
        Assert.NotNull(s.FocalLength);Assert.NotNull(s.FNumber);Assert.NotNull(s.FocalLength35mm);
        Assert.Equal(100,s.Iso);Assert.Equal(1d/250,s.ExposureSeconds);Assert.Equal(0,s.ExposureBias);
        Assert.Equal(16,s.Flash);Assert.Equal(0,s.WhiteBalance);Assert.NotEmpty(s.Software);
        Assert.True(s.UseExportDateWhenMissing);Assert.Null(s.Latitude);Assert.Empty(new PresetService().Validate(s));
        if(model is "iPhone 16" or "iPhone 16e")Assert.Equal("Main",s.Camera);
    }
    [Theory]
    [InlineData(".jpg")] [InlineData(".png")] [InlineData(".webp")] [InlineData(".mp4")] [InlineData(".mov")]
    public async Task MissingCaptureDateUsesBatchFallbackAndKeepsOriginalDateWhenPresent(string extension)
    {
        using var temp=new TempDirectory();
        var source=await ProcessingTests.CreateFixture(temp,extension);
        var video=extension is ".mp4" or ".mov";
        await new ToolRunner().RunAsync(ProcessingTests.Paths.ExifTool,["-overwrite_original","-all=",source],default);
        var date=DateTimeOffset.Parse("2025-03-04T05:06:07+06:00");
        var s=AutoPresetService.Create("iPhone 16","Main") with{FallbackCaptureDate=date,OutputDirectory=System.IO.Path.Combine(temp.Path,"fallback")};
        var result=await new ProcessingService(ProcessingTests.Paths).ExportAsync(new(source,video?MediaKind.Video:MediaKind.Image,1),s,new OutputAllocator(s.OutputDirectory,true,1),default);
        Assert.True(result.Success,result.Error);
        var data=await new MetadataReader(ProcessingTests.Paths).ReadAsync(result.Path!,default);
        Assert.Equal("2025:03:04 05:06:07"+(video?"+06:00":""),data.Get(video?"CreationDate":"DateTimeOriginal"));
        var original="2024:01:02 03:04:05"+(video?"+06:00":"");
        await new ToolRunner().RunAsync(ProcessingTests.Paths.ExifTool,["-overwrite_original",(video?"-Keys:CreationDate=":"-EXIF:DateTimeOriginal=")+original,source],default);
        s=s with{OutputDirectory=System.IO.Path.Combine(temp.Path,"original")};
        result=await new ProcessingService(ProcessingTests.Paths).ExportAsync(new(source,video?MediaKind.Video:MediaKind.Image,1),s,new OutputAllocator(s.OutputDirectory,true,1),default);
        Assert.True(result.Success,result.Error);
        data=await new MetadataReader(ProcessingTests.Paths).ReadAsync(result.Path!,default);
        Assert.Equal(original,data.Get(video?"CreationDate":"DateTimeOriginal"));
    }
    [Fact]
    public void AutoDatePolicyPersistsButRuntimeTimestampDoesNot()
    {
        using var temp=new TempDirectory();var store=new SavedPresetStore(System.IO.Path.Combine(temp.Path,"presets.json"));
        var s=AutoPresetService.Create("iPhone 16","Main");store.Save("Auto",s with{FallbackCaptureDate=DateTimeOffset.Now});
        Assert.Equal(s,Assert.Single(store.Load()).Settings);
    }
}
