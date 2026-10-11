namespace MetaRemover.Core;
public sealed class PresetService
{
    public static readonly string[] Models = ["iPhone 11", "iPhone 11 Pro", "iPhone 11 Pro Max", "iPhone 12", "iPhone 12 mini", "iPhone 12 Pro", "iPhone 12 Pro Max", "iPhone 13", "iPhone 13 mini", "iPhone 13 Pro", "iPhone 13 Pro Max", "iPhone 14", "iPhone 14 Plus", "iPhone 14 Pro", "iPhone 14 Pro Max", "iPhone 15", "iPhone 15 Plus", "iPhone 15 Pro", "iPhone 15 Pro Max", "iPhone 16", "iPhone 16 Plus", "iPhone 16 Pro", "iPhone 16 Pro Max", "iPhone 16e", "iPhone SE", "iPhone SE (2nd generation)", "iPhone SE (3rd generation)"];
    public IReadOnlyList<string> Validate(ExportSettings s)
    {
        var errors = new List<string>();
        if (!s.RemoveOnly && (string.IsNullOrWhiteSpace(s.Model) || s.Model.Length > 120 || s.Model.Any(char.IsControl))) errors.Add("Enter a valid iPhone model (maximum 120 characters).");
        if (s.Software.Length > 120 || s.Software.Any(char.IsControl)) errors.Add("Software must be plain text (maximum 120 characters).");
        if (s.Latitude.HasValue != s.Longitude.HasValue) errors.Add("Enter both latitude and longitude.");
        if (s.Latitude is double lat && (!double.IsFinite(lat) || lat < -90 || lat > 90)) errors.Add("Latitude must be between -90 and 90.");
        if (s.Longitude is double lon && (!double.IsFinite(lon) || lon < -180 || lon > 180)) errors.Add("Longitude must be between -180 and 180.");
        if (s.StartNumber is < 1 or > 9999) errors.Add("Starting number must be 0001–9999.");
        foreach(var (label,value) in new[]{("Lens make",s.LensMake),("Lens model",s.LensModel),("Camera",s.Camera)})
            if(value is null||value.Length>160||value.Any(char.IsControl))errors.Add(label+" must be plain text (maximum 160 characters).");
        foreach(var (label,value,min,max) in new[]{("Aperture",s.FNumber,0.1,128d),("Physical focal length",s.FocalLength,0.1,2000d),("Shutter seconds",s.ExposureSeconds,0.000001,3600d),("Exposure compensation",s.ExposureBias,-20d,20d)})
            if(value is double n&&(!double.IsFinite(n)||n<min||n>max))errors.Add($"{label} must be between {min} and {max}.");
        if(s.FocalLength35mm is <1 or >65535)errors.Add("Equivalent focal length must be 1–65535 mm.");
        if(s.Iso is <1 or >65535)errors.Add("ISO must be 1–65535.");
        if(s.Flash.HasValue&&s.Flash is not (0 or 1 or 16 or 24 or 25))errors.Add("Choose a supported flash setting.");
        if(s.WhiteBalance.HasValue&&s.WhiteBalance is not (0 or 1))errors.Add("Choose Auto or Manual white balance.");
        return errors;
    }
    public IReadOnlyDictionary<string,string> BuildTags(MediaKind kind, ExportSettings s)
    {
        var errors = Validate(s);
        if (errors.Count != 0) throw new ArgumentException(string.Join(" ", errors));
        var tags = new Dictionary<string,string>();
        if (s.RemoveOnly) return tags;
        var prefix = kind == MediaKind.Video ? "Keys:" : "EXIF:";
        tags[prefix + "Make"] = "Apple";
        tags[prefix + "Model"] = s.Model.Trim();
        if (!string.IsNullOrWhiteSpace(s.Software)) tags[prefix + "Software"] = s.Software.Trim();
        if(kind==MediaKind.Image)
        {
            if(!string.IsNullOrWhiteSpace(s.LensMake))tags["EXIF:LensMake"]=s.LensMake.Trim();
            if(!string.IsNullOrWhiteSpace(s.LensModel))tags["EXIF:LensModel"]=s.LensModel.Trim();
            void Number(string tag,double? value){if(value.HasValue)tags["EXIF:"+tag]=value.Value.ToString("G",System.Globalization.CultureInfo.InvariantCulture);}
            Number("FNumber",s.FNumber);Number("FocalLength",s.FocalLength);Number("FocalLengthIn35mmFormat",s.FocalLength35mm);
            Number("ISO",s.Iso);Number("ExposureTime",s.ExposureSeconds);Number("ExposureCompensation",s.ExposureBias);
            Number("Flash#",s.Flash);Number("WhiteBalance#",s.WhiteBalance);
        }
        else
        {
            if(!string.IsNullOrWhiteSpace(s.LensModel))tags["VideoKeys:LensModel"]=s.LensModel.Trim();
            if(s.FocalLength35mm.HasValue)tags["VideoKeys:FocalLengthIn35mmFormat"]=s.FocalLength35mm.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        if (s.CaptureDate is DateTimeOffset date)
        {
            if (kind == MediaKind.Video)
            {
                tags["Keys:CreationDate"] = date.ToString("yyyy:MM:dd HH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);
                foreach (var key in new[] { "CreateDate", "ModifyDate", "TrackCreateDate", "TrackModifyDate", "MediaCreateDate", "MediaModifyDate" })
                    tags["QuickTime:" + key] = date.ToUniversalTime().ToString("yyyy:MM:dd HH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                foreach (var key in new[] { "DateTimeOriginal", "CreateDate", "ModifyDate" }) tags["EXIF:" + key] = date.ToString("yyyy:MM:dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                foreach (var key in new[] { "OffsetTimeOriginal", "OffsetTimeDigitized", "OffsetTime" }) tags["EXIF:" + key] = date.ToString("zzz", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        if (s.Latitude is double lat && s.Longitude is double lon)
        {
            if (kind == MediaKind.Video) tags["Keys:GPSCoordinates"] = FormattableString.Invariant($"{lat:F6} {lon:F6}");
            else
            {
                tags["EXIF:GPSLatitude"] = Math.Abs(lat).ToString(System.Globalization.CultureInfo.InvariantCulture);
                tags["EXIF:GPSLatitudeRef"] = lat < 0 ? "S" : "N";
                tags["EXIF:GPSLongitude"] = Math.Abs(lon).ToString(System.Globalization.CultureInfo.InvariantCulture);
                tags["EXIF:GPSLongitudeRef"] = lon < 0 ? "W" : "E";
            }
        }
        return tags;
    }
}
