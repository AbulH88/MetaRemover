using MetaRemover.Core;
namespace MetaRemover.Tests;
public class SavedPresetTests
{
    [Fact]
    public void NullSettingsFieldsAreReportedWithoutOverwritingStorage()
    {
        using var temp = new TempDirectory();
        var path=System.IO.Path.Combine(temp.Path,"presets.json");
        var json="[{\"Name\":\"Broken\",\"Settings\":{\"Software\":null}}]";
        System.IO.File.WriteAllText(path,json);
        Assert.Throws<ArgumentException>(()=>new SavedPresetStore(path).Load());
        Assert.Equal(json,System.IO.File.ReadAllText(path));
    }
    [Fact]
    public void SaveReloadUpdateDeletePreservesAllSettingsExceptDestination()
    {
        using var temp = new TempDirectory();
        var path = System.IO.Path.Combine(temp.Path, "presets.json");
        var settings = new ExportSettings { Model="iPhone 15 Pro", Software="18.1", KeepOriginalDate=false,
            CaptureDate=new DateTimeOffset(2025,1,2,3,4,5,TimeSpan.FromHours(6)), Latitude=23.8, Longitude=90.4,
            IphoneNames=false, StartNumber=45, PreserveFolders=true, AllowExtraTrackRemoval=true, OutputDirectory="machine-specific" };
        new SavedPresetStore(path).Save("Travel",settings);
        var store = new SavedPresetStore(path);
        Assert.Equal(settings with {OutputDirectory=""}, Assert.Single(store.Load()).Settings);
        store.Save("travel", settings with {Software="19.0"});
        Assert.Equal("19.0", Assert.Single(store.Load()).Settings.Software);
        store.Delete("TRAVEL");
        Assert.Empty(new SavedPresetStore(path).Load());
    }
    [Fact]
    public void BadInputAndMalformedStorageDoNotOverwriteSavedData()
    {
        using var temp = new TempDirectory();
        var path = System.IO.Path.Combine(temp.Path,"presets.json");
        var store = new SavedPresetStore(path);
        store.Save("Good",new ExportSettings());
        var before = System.IO.File.ReadAllText(path);
        Assert.Throws<ArgumentException>(()=>store.Save(" ",new ExportSettings()));
        Assert.Throws<ArgumentException>(()=>store.Save("Bad",new ExportSettings{Latitude=100,Longitude=0}));
        Assert.Equal(before,System.IO.File.ReadAllText(path));
        System.IO.File.WriteAllText(path,"broken json");
        Assert.Throws<System.Text.Json.JsonException>(()=>store.Save("New",new ExportSettings()));
        Assert.Equal("broken json",System.IO.File.ReadAllText(path));
    }
}
