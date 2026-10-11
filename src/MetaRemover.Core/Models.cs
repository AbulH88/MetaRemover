namespace MetaRemover.Core;

public enum MediaKind { Image, Video }
public record MediaFile(string Path, MediaKind Kind, long Bytes)
{
    public string RelativeFolder { get; init; } = "";
}
public record ImportResult(IReadOnlyList<MediaFile> Files, IReadOnlyList<string> Errors);
public record ExportSettings
{
    public bool RemoveOnly { get; init; }
    public string Model { get; init; } = "iPhone 16 Pro";
    public string Software { get; init; } = "";
    public string Camera { get; init; } = "Custom / none";
    public string LensMake { get; init; } = "";
    public string LensModel { get; init; } = "";
    public double? FNumber { get; init; }
    public double? FocalLength { get; init; }
    public int? FocalLength35mm { get; init; }
    public int? Iso { get; init; }
    public double? ExposureSeconds { get; init; }
    public double? ExposureBias { get; init; }
    public int? Flash { get; init; }
    public int? WhiteBalance { get; init; }
    public DateTimeOffset? CaptureDate { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string OutputDirectory { get; init; } = "";
    public bool IphoneNames { get; init; } = true;
    public int StartNumber { get; init; } = 1;
    public bool PreserveFolders { get; init; }
    public bool AllowExtraTrackRemoval { get; init; }
    public bool KeepOriginalDate { get; init; } = true;
    public bool UseExportDateWhenMissing { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public DateTimeOffset? FallbackCaptureDate { get; init; }
}
