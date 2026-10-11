namespace MetaRemover.Core;
public static class AutoPresetService
{
    // Editable daylight examples, not capture measurements. Physical mm examples are
    // calibrated against ExifTool Apple samples and Apple's camera-lens metadata example.
    public static ExportSettings Create(string model,string camera)
    {
        var cameras=CameraCatalog.ForModel(model);
        var c=cameras.FirstOrDefault(c=>c.Name==camera)??cameras.FirstOrDefault(c=>c.Name=="Main")
            ??throw new ArgumentException("Choose a built-in iPhone model for automatic presets.");
        var mainMm=model switch
        {
            "iPhone SE"=>4.15,"iPhone SE (2nd generation)" or "iPhone SE (3rd generation)"=>3.99,
            "iPhone 11" or "iPhone 11 Pro" or "iPhone 11 Pro Max"=>4.25,
            "iPhone 12" or "iPhone 12 mini" or "iPhone 12 Pro" or "iPhone 16e"=>4.2,
            "iPhone 12 Pro Max" or "iPhone 13" or "iPhone 13 mini"=>5.1,
            "iPhone 13 Pro" or "iPhone 13 Pro Max" or "iPhone 14" or "iPhone 14 Plus"=>5.7,
            "iPhone 14 Pro" or "iPhone 14 Pro Max" or "iPhone 15 Pro"=>6.86,
            "iPhone 15 Pro Max" or "iPhone 16 Pro" or "iPhone 16 Pro Max"=>6.765,
            _=>5.96
        };
        var physical=c.Name switch{"Main"=>mainMm,"Front"=>model=="iPhone SE"?2.15:2.69,"Ultra Wide"=>model.Contains("Pro")&&c.Aperture==2.2?2.22:1.54,_=>c.EquivalentMm switch{120=>15.66,77=>9,65=>7.5,_=>6}};
        var equivalent=c.EquivalentMm??(c.Name=="Front"?23:model=="iPhone SE"?29:28);
        return new(){Model=model,Camera=c.Name,LensMake="Apple",LensModel=model+" "+c.Name.ToLowerInvariant()+" camera",
            FNumber=c.Aperture,FocalLength=physical,FocalLength35mm=equivalent,Iso=100,ExposureSeconds=1d/250,ExposureBias=0,
            Flash=16,WhiteBalance=0,Software=model=="iPhone SE"?"15.8":model=="iPhone 16e"?"18.3":"18.1",KeepOriginalDate=true,UseExportDateWhenMissing=true};
    }
}
