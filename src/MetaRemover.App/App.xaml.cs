using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using MetaRemover.Core;

namespace MetaRemover.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        var index=Array.IndexOf(e.Args,"--smoke-test");
        if(index<0) { base.OnStartup(e); new MainWindow().Show(); return; }
        ShutdownMode=ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);
        string? report=null;
        try
        {
            if(index+1>=e.Args.Length)throw new ArgumentException("Provide a smoke-test working directory.");
            var working=Path.GetFullPath(e.Args[index+1]);
            Directory.CreateDirectory(working);
            report=Path.Combine(working,"smoke-result.json");
            var run=Path.Combine(working,"run-"+Guid.NewGuid().ToString("N"));
            var sourceDir=Path.Combine(run,"source");Directory.CreateDirectory(sourceDir);
            var tools=ToolPaths.Bundled(AppContext.BaseDirectory);
            var runner=new ToolRunner();
            foreach(var extension in new[]{".jpg",".png",".webp",".mp4",".mov"})
            {
                var file=Path.Combine(sourceDir,"বাংলা sample"+extension);
                List<string> args=["-v","error","-f","lavfi","-i","testsrc2=s=160x96:r=5"];
                if(extension is ".mp4" or ".mov")args.AddRange(["-t","0.6","-c:v","mpeg4"]);
                else args.AddRange(["-frames:v","1","-threads","1","-update","1"]);
                args.Add(file);
                await runner.RunAsync(tools.Ffmpeg,args,default);
                await runner.RunAsync(tools.ExifTool,extension is ".mp4" or ".mov"
                    ?["-overwrite_original","-Keys:Make=Canon","-Keys:Model=Test camera","-Keys:GPSCoordinates=23.8 90.4",file]
                    :["-overwrite_original","-Make=Canon","-Model=Test camera","-GPSLatitude=23.8","-GPSLatitudeRef=N","-GPSLongitude=90.4","-GPSLongitudeRef=E",file],default);
            }
            var inputs=Directory.GetFiles(sourceDir);
            var hashes=inputs.ToDictionary(p=>p,p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
            var model=new WorkspaceModel(tools);
            await model.ImportAsync(inputs,false,null);
            var replace=new ExportSettings{KeepOriginalDate=false,CaptureDate=DateTimeOffset.Parse("2026-10-11T19:30:00+06:00"),
                OutputDirectory=Path.Combine(run,"replace"),Latitude=-33.86,Longitude=151.2,StartNumber=4827};
            await model.ExportAsync(false,replace);
            if(model.Completed!=5||model.Failed!=0)throw new IOException("Replacement batch failed: "+string.Join(" | ",model.Files.Select(f=>f.Detail)));
            foreach(var item in model.Files)
            {
                var actual=await new MetadataReader(tools).ReadAsync(item.Result!.Path!,default);
                if(actual.Get("Model")!="iPhone 16 Pro")throw new IOException("Replacement model verification failed.");
            }
            await model.ExportAsync(false,replace with{RemoveOnly=true,OutputDirectory=Path.Combine(run,"remove"),StartNumber=1,Latitude=null,Longitude=null});
            if(model.Completed!=5||model.Failed!=0)throw new IOException("Removal batch failed: "+string.Join(" | ",model.Files.Select(f=>f.Detail)));
            foreach(var item in model.Files)
            {
                var actual=await new MetadataReader(tools).ReadAsync(item.Result!.Path!,default);
                if(actual.Tags.Keys.Any(k=>k.Contains("GPS")||k.EndsWith(":Model")))throw new IOException("Removal metadata verification failed.");
                if(hashes[item.File.Path]!=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(item.File.Path))))throw new IOException("Original file changed.");
            }
            await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new{Success=true,Checks=10,Formats=new[]{"JPG","PNG","WebP","MP4","MOV"},OriginalsUnchanged=true,PortableTools=true,RunDirectory=run},new JsonSerializerOptions{WriteIndented=true}));
            Shutdown(0);
        }
        catch(Exception ex)
        {
            if(report!=null)await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new{Success=false,Error=ex.ToString()},new JsonSerializerOptions{WriteIndented=true}));
            Shutdown(1);
        }
    }
}
