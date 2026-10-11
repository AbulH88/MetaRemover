namespace MetaRemover.Core;
public record CameraProfile(string Name,double Aperture,int? EquivalentMm);
public static class CameraCatalog
{
    // Hardware apertures and 35mm equivalents from Apple's technical specifications.
    // Physical focal length is a separate, optional field; never substitute equivalent mm.
    public static IReadOnlyList<CameraProfile> ForModel(string model)
    {
        if(!PresetService.Models.Contains(model))return [];
        if(model.StartsWith("iPhone SE"))return [new("Main",model=="iPhone SE"?2.2:1.8,null),new("Front",model=="iPhone SE"?2.4:2.2,null)];
        var generation=int.Parse(model.Split(' ')[1].TrimEnd('e'));
        var pro=model.Contains("Pro");var max=model.Contains("Max");
        var main=generation switch{11=>1.8,12=>1.6,13=>pro?1.5:1.6,14=>pro?1.78:1.5,_=>pro?1.78:1.6};
        var cameras=new List<CameraProfile>{new("Main",main,generation>=14&&pro?24:26)};
        if(model!="iPhone 16e")cameras.Add(new("Ultra Wide",generation>=14&&pro||generation==16?2.2:generation==13&&pro?1.8:2.4,13));
        if(pro)cameras.Add(new("Telephoto",generation switch{11=>2,12=>max?2.2:2,_=>2.8},generation switch{11=>52,12=>max?65:52,13 or 14=>77,15=>max?120:77,_=>120}));
        cameras.Add(new("Front",generation>=14?1.9:2.2,null));
        return cameras;
    }
}
