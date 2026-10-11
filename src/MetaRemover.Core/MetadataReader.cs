using System.Globalization;
using System.Text.Json;
namespace MetaRemover.Core;
public record ToolPaths(string ExifTool, string Ffmpeg, string Ffprobe)
{
    public static ToolPaths Bundled(string root) => new(Path.Combine(root,"tools/exiftool/ExifTool.exe"),Path.Combine(root,"tools/ffmpeg/ffmpeg.exe"),Path.Combine(root,"tools/ffmpeg/ffprobe.exe"));
}
public record VideoStream(int Index, string Type, string Codec, int Width, int Height, string ColorSpace, string ColorTransfer, string ColorPrimaries, double Rotation);
public record MediaInspection(IReadOnlyDictionary<string,string> Tags,int Width,int Height,double Duration,IReadOnlyList<VideoStream> Streams)
{
    public bool HasExtraTracks => Streams.Any(s => s.Type is not ("audio" or "video"));
    public string? Get(string tag) => Tags.FirstOrDefault(t => t.Key.EndsWith(":"+tag,StringComparison.OrdinalIgnoreCase)).Value;
}
public sealed class MetadataReader(ToolPaths paths)
{
    private readonly ToolRunner runner = new();
    public async Task<MediaInspection> ReadAsync(string path,CancellationToken token)
    {
        var result = await runner.RunAsync(paths.ExifTool,["-json","-G1","-s","-n","-api","QuickTimeUTC=1","-charset","filename=UTF8","--",Path.GetFullPath(path)],token);
        using var json = JsonDocument.Parse(result.Output);
        var tags = json.RootElement[0].EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.ValueKind==JsonValueKind.String?p.Value.GetString()??"":p.Value.ToString());
        var error = tags.FirstOrDefault(p=>p.Key.EndsWith(":Error")||p.Key=="Error").Value;
        if(error!=null) throw new IOException(error);
        string? Get(string name)=>tags.FirstOrDefault(p=>p.Key.EndsWith(":"+name)).Value;
        int.TryParse(Get("ImageWidth"),out var width); int.TryParse(Get("ImageHeight"),out var height);
        if(!ImportService.VideoExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
        {
            if(width<=0||height<=0) throw new IOException("The image is corrupt or its dimensions cannot be read.");
            return new(tags,width,height,0,[]);
        }
        var probe=await runner.RunAsync(paths.Ffprobe,["-v","error","-show_streams","-show_format","-of","json",path],token);
        using var pj=JsonDocument.Parse(probe.Output); var root=pj.RootElement;
        var streams=new List<VideoStream>();
        foreach(var s in root.GetProperty("streams").EnumerateArray())
        {
            string Str(string key)=>s.TryGetProperty(key,out var v)?v.ToString():"";
            int Num(string key)=>s.TryGetProperty(key,out var v)&&v.TryGetInt32(out var n)?n:0;
            double rotation=0;
            if(s.TryGetProperty("side_data_list",out var side)) foreach(var item in side.EnumerateArray()) if(item.TryGetProperty("rotation",out var r)) rotation=r.GetDouble();
            streams.Add(new(Num("index"),Str("codec_type"),Str("codec_name"),Num("width"),Num("height"),Str("color_space"),Str("color_transfer"),Str("color_primaries"),rotation));
        }
        var main=streams.FirstOrDefault(s=>s.Type=="video")??throw new IOException("No video stream was found.");
        double duration=0;
        if(root.TryGetProperty("format",out var format)&&format.TryGetProperty("duration",out var d)) double.TryParse(d.ToString(),CultureInfo.InvariantCulture,out duration);
        return new(tags,main.Width,main.Height,duration,streams);
    }
}
