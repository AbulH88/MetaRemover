using System.Globalization;
using System.Text.RegularExpressions;
namespace MetaRemover.Core;
public sealed class VerificationService(ToolPaths paths)
{
    public async Task<IReadOnlyList<string>> VerifyAsync(MediaInspection source,string stagedPath,ExportSettings settings,IReadOnlyDictionary<string,string> expected,CancellationToken token)
    {
        var output=await new MetadataReader(paths).ReadAsync(stagedPath,token);
        if(output.Width!=source.Width||output.Height!=source.Height) throw new IOException("Output dimensions changed; export was not published.");
        var a=source.Streams.Where(s=>s.Type is "audio" or "video").ToArray(); var b=output.Streams.Where(s=>s.Type is "audio" or "video").ToArray();
        if(a.Length!=b.Length) throw new IOException("An audio or video stream was lost.");
        for(int i=0;i<a.Length;i++) if((a[i] with {Index=0})!=(b[i] with {Index=0})) throw new IOException("Codec, orientation, or color signaling changed; export was not published.");
        if(a.Length>0&&Math.Abs(source.Duration-output.Duration)>0.15) throw new IOException("Playback duration changed; export was not published.");
        if(source.Get("Orientation")!=null&&source.Get("Orientation")!=output.Get("Orientation")) throw new IOException("Image orientation could not be preserved.");
        if(source.Get("ProfileDescription")!=output.Get("ProfileDescription")) throw new IOException("The image color profile could not be preserved.");
        foreach(var (key,value) in expected)
        {
            var tag=key[(key.IndexOf(':')+1)..].TrimEnd('#'); var actual=output.Get(tag);
            if(tag=="GPSCoordinates")
            {
                var latText=output.Tags.FirstOrDefault(k=>k.Key=="Composite:GPSLatitude").Value??output.Get("GPSLatitude");
                var lonText=output.Tags.FirstOrDefault(k=>k.Key=="Composite:GPSLongitude").Value??output.Get("GPSLongitude");
                if(!double.TryParse(latText,CultureInfo.InvariantCulture,out var lat)||!double.TryParse(lonText,CultureInfo.InvariantCulture,out var lon)||Math.Abs(lat-settings.Latitude!.Value)>0.00001||Math.Abs(lon-settings.Longitude!.Value)>0.00001) throw new IOException("GPS values were not written correctly.");
            }
            else if(tag is "GPSLatitude" or "GPSLongitude")
            {
                if(!double.TryParse(actual,CultureInfo.InvariantCulture,out var n)||Math.Abs(Math.Abs(n)-double.Parse(value,CultureInfo.InvariantCulture))>0.00001) throw new IOException("A GPS tag could not be verified: "+tag);
            }
            else if(key.StartsWith("QuickTime:")&&tag.EndsWith("Date"))
            {
                var parsedExpected = DateTimeOffset.TryParseExact(value, "yyyy:MM:dd HH:mm:sszzz", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expectedDate);
                var parsedActual = DateTimeOffset.TryParseExact(actual, "yyyy:MM:dd HH:mm:sszzz", CultureInfo.InvariantCulture, DateTimeStyles.None, out var actualDate);
                if (parsedExpected ? !parsedActual || expectedDate.UtcDateTime != actualDate.UtcDateTime : actual != value)
                    throw new IOException("A video date could not be verified: "+tag);
            }
            else if(tag is "FNumber" or "FocalLength" or "FocalLengthIn35mmFormat" or "ISO" or "ExposureTime" or "ExposureCompensation" or "Flash" or "WhiteBalance")
            {
                if(!double.TryParse(actual,CultureInfo.InvariantCulture,out var n)||!double.TryParse(value,CultureInfo.InvariantCulture,out var requested)||Math.Abs(n-requested)>Math.Max(0.000001,Math.Abs(requested)*0.00001))
                    throw new IOException("A numeric camera tag could not be verified: "+tag);
            }
            else if(actual!=value) throw new IOException("A requested tag could not be verified: "+tag);
        }
        var warnings=new List<string>();
        if(source.HasExtraTracks) warnings.Add("Auxiliary/subtitle/data tracks were excluded as requested.");
        var permitted=expected.Keys.Select(k=>k[(k.IndexOf(':')+1)..].TrimEnd('#')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sensitive=new Regex("GPS|Serial|Owner|Artist|Author|Comment|Description|Copyright|Make$|Model$|Software$|DateTimeOriginal$",RegexOptions.IgnoreCase);
        var residual=output.Tags.Keys.Where(k=>!k.StartsWith("System:")&&!k.StartsWith("File:")&&!k.StartsWith("Composite:")&&!k.StartsWith("ICC")&&sensitive.IsMatch(k)&&!permitted.Contains(k[(k.IndexOf(':')+1)..])).ToArray();
        residual = residual.Where(k => !k.EndsWith(":HandlerDescription") || output.Tags[k] is not ("VideoHandler" or "SoundHandler" or "DataHandler")).ToArray();
        if(settings.Latitude.HasValue) residual=residual.Where(k=>!k.Contains("GPS",StringComparison.OrdinalIgnoreCase)).ToArray();
        if(residual.Length>0) warnings.Add("Review remaining descriptive tags: "+string.Join(", ",residual.Select(k => k + "=" + output.Tags[k])));
        return warnings;
    }
}
