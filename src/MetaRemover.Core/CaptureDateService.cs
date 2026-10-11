namespace MetaRemover.Core;
public static class CaptureDateService
{
    public static bool HasOriginal(MediaInspection source,MediaKind kind)
        => (kind==MediaKind.Image?new[]{"DateTimeOriginal","CreateDate"}:new[]{"CreationDate","CreateDate"})
            .Any(tag=>source.Get(tag) is string value&&value.Length>=19&&value[0]!='0');
    public static ExportSettings Resolve(ExportSettings settings,MediaInspection source,MediaKind kind)
        => !settings.RemoveOnly&&settings.KeepOriginalDate&&settings.CaptureDate is null&&settings.UseExportDateWhenMissing&&!HasOriginal(source,kind)
            ?settings with {CaptureDate=settings.FallbackCaptureDate??DateTimeOffset.Now,KeepOriginalDate=false}:settings;
}
