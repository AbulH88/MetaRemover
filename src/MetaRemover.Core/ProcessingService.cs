namespace MetaRemover.Core;
public record ExportResult(bool Success,string? Path,string? Error,IReadOnlyList<string> Warnings);
public sealed class ProcessingService(ToolPaths paths)
{
    private readonly ToolRunner runner=new();
    private readonly MetadataReader reader=new(paths);
    public async Task<ExportResult> ExportAsync(MediaFile file,ExportSettings settings,OutputAllocator allocator,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            var errors=new PresetService().Validate(settings);
            if(errors.Count>0) throw new ArgumentException(string.Join(" ",errors));
            var source=await reader.ReadAsync(file.Path,token);
            settings=CaptureDateService.Resolve(settings,source,file.Kind);
            if(source.HasExtraTracks&&!settings.AllowExtraTrackRemoval) throw new IOException("This video has auxiliary/subtitle/data tracks. Inspect it and enable removal of extra tracks before exporting.");
            using var output=allocator.Reserve(file.Path,settings.PreserveFolders?file.RelativeFolder:null);
            if(file.Kind==MediaKind.Image) File.Copy(file.Path,output.StagingPath,false);
            else await runner.RunAsync(paths.Ffmpeg,["-hide_banner","-loglevel","error","-nostdin","-i",file.Path,"-map","0:v","-map","0:a?","-c","copy","-map_metadata","-1","-map_metadata:s","-1","-metadata:s:v","handler_name=VideoHandler","-metadata:s:a","handler_name=SoundHandler","-map_chapters","-1","-fflags","+bitexact","-flags:v","+bitexact",output.StagingPath],token);
            var tags=new PresetService().BuildTags(file.Kind,settings).ToDictionary();
            if(!settings.RemoveOnly&&settings.KeepOriginalDate&&settings.CaptureDate==null)
            {
                if(file.Kind==MediaKind.Image)
                {
                    foreach(var name in new[]{"DateTimeOriginal","CreateDate","ModifyDate","OffsetTime","OffsetTimeOriginal","OffsetTimeDigitized"}) if(source.Get(name) is string value) tags["EXIF:"+name]=value;
                }
                else
                {
                    if(source.Get("CreationDate") is string date) tags["Keys:CreationDate"]=date;
                    foreach(var name in new[]{"CreateDate","ModifyDate","TrackCreateDate","TrackModifyDate","MediaCreateDate","MediaModifyDate"}) if(source.Get(name) is string value) tags["QuickTime:"+name]=value;
                }
            }
            List<string> args=["-overwrite_original","-api","QuickTimeUTC=1","-charset","filename=UTF8","-all="];
            if(file.Kind==MediaKind.Image)
            {
                var critical = new List<string>();
                if (source.Tags.Keys.Any(k => k.StartsWith("ICC"))) critical.Add("-ICC_Profile");
                if (source.Get("Orientation") != null) critical.Add("-Orientation");
                if (critical.Count > 0) { args.AddRange(["-tagsFromFile",file.Path]); args.AddRange(critical); }
            }
            else foreach(var name in new[]{"CreateDate","ModifyDate","TrackCreateDate","TrackModifyDate","MediaCreateDate","MediaModifyDate"}) args.Add("-QuickTime:"+name+"=");
            args.AddRange(tags.Select(kv=>"-"+kv.Key+"="+kv.Value)); args.Add("--"); args.Add(output.StagingPath);
            var write=await runner.RunAsync(paths.ExifTool,args,token);
            var warnings=(await new VerificationService(paths).VerifyAsync(source,output.StagingPath,settings,tags,token)).ToList();
            if(write.Error.Contains("Warning",StringComparison.OrdinalIgnoreCase)) warnings.Add(write.Error.Trim());
            token.ThrowIfCancellationRequested();
            File.SetCreationTimeUtc(output.StagingPath,DateTime.UtcNow); File.SetLastWriteTimeUtc(output.StagingPath,DateTime.UtcNow);
            output.Publish();
            return new(true,output.FinalPath,null,warnings);
        }
        catch(OperationCanceledException){throw;}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException)
        {return new(false,null,e.Message,[]);}
    }
}
