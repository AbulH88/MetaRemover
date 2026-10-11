using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MetaRemover.Core;
namespace MetaRemover.App;
public record CameraOption(string Name,int? Value);
public partial class MainWindow
{
    private string cameraModel="";
    private static readonly CultureInfo Invariant=CultureInfo.InvariantCulture;
    private void InitializeCameraControls()
    {
        FlashPicker.ItemsSource=new CameraOption[]{new("Not set",null),new("Did not fire",0),new("Fired",1),new("Off",16),new("Auto, did not fire",24),new("Auto, fired",25)};
        WhiteBalancePicker.ItemsSource=new CameraOption[]{new("Not set",null),new("Auto",0),new("Manual",1)};
        FlashPicker.SelectedIndex=0;WhiteBalancePicker.SelectedIndex=0;
        SetCameraChoices("Main");FillCameraHardware();
    }
    private void SetCameraChoices(string camera)
    {
        cameraModel=ModelPicker.Text.Trim();
        var choices=new[]{"Custom / none"}.Concat(CameraCatalog.ForModel(cameraModel).Select(c=>c.Name)).ToArray();
        CameraPicker.ItemsSource=choices;
        CameraPicker.SelectedItem=choices.Contains(camera)?camera:choices.Contains("Main")?"Main":"Custom / none";
    }
    private void ScheduleCameraModelChange()
    {
        Dispatcher.BeginInvoke(new Action(()=>
        {
            if(!ready||cameraModel==ModelPicker.Text.Trim())return;
            var camera=CameraPicker.SelectedItem?.ToString()??"Main";
            if(camera=="Custom / none")camera="Main";
            ready=false;
            try{SetCameraChoices(camera);FillCameraHardware();}
            finally{ready=true;}
            RefreshPreview();
        }));
    }
    private void Camera_Changed(object sender,SelectionChangedEventArgs e)
    {
        if(!ready)return;
        ready=false;
        try{FillCameraHardware();}finally{ready=true;}
        RefreshPreview();
    }
    private void Autofill_Click(object sender,RoutedEventArgs e)
    {
        ready=false;
        try{SetCameraChoices(CameraPicker.SelectedItem?.ToString() is string name&&name!="Custom / none"?name:"Main");FillCameraHardware();}
        finally{ready=true;}
        RefreshPreview();
    }
    private void FillCameraHardware()
    {
        var camera=CameraPicker.SelectedItem?.ToString();
        var hardware=CameraCatalog.ForModel(cameraModel).FirstOrDefault(c=>c.Name==camera);
        LensMakeInput.Text=hardware is null?"":"Apple";
        LensModelInput.Text=hardware is null?"":cameraModel+" "+hardware.Name.ToLowerInvariant()+" camera";
        ApertureInput.Text=hardware?.Aperture.ToString(Invariant)??"";
        EquivalentInput.Text=hardware?.EquivalentMm?.ToString(Invariant)??"";
        PhysicalInput.Text="";
        if(hardware is not null)
        {
            var defaults=AutoPresetService.Create(cameraModel,hardware.Name);
            ApplyCameraSettings(defaults);
            SoftwareInput.Text=defaults.Software;
            KeepDate.IsChecked=true;DateFallback.IsChecked=true;
            DateInput.IsEnabled=false;OffsetPicker.IsEnabled=false;
        }
    }
    private static double? OptionalNumber(string text,string label)
    {
        if(string.IsNullOrWhiteSpace(text))return null;
        if(!double.TryParse(text.Trim(),NumberStyles.Float,Invariant,out var n)||!double.IsFinite(n))throw new ArgumentException("Enter a numeric "+label+" or leave it blank.");
        return n;
    }
    private static int? OptionalInteger(string text,string label)
    {
        if(string.IsNullOrWhiteSpace(text))return null;
        if(!int.TryParse(text.Trim(),NumberStyles.Integer,Invariant,out var n))throw new ArgumentException("Enter a whole number for "+label+" or leave it blank.");
        return n;
    }
    private ExportSettings ReadCameraSettings(ExportSettings s)
    {
        if(s.RemoveOnly)return s;
        double? shutter;
        var parts=ShutterInput.Text.Trim().Split('/');
        if(parts.Length==1)shutter=OptionalNumber(parts[0],"shutter speed");
        else if(parts.Length==2&&OptionalNumber(parts[0],"shutter numerator") is double numerator&&OptionalNumber(parts[1],"shutter denominator") is double denominator&&denominator>0)
            shutter=numerator/denominator;
        else throw new ArgumentException("Shutter speed format: 1/320 or seconds, e.g. 0.01.");
        return s with {Camera=CameraPicker.SelectedItem?.ToString()??"Custom / none",LensMake=LensMakeInput.Text,LensModel=LensModelInput.Text,
            FNumber=OptionalNumber(ApertureInput.Text,"aperture"),FocalLength=OptionalNumber(PhysicalInput.Text,"physical focal length"),FocalLength35mm=OptionalInteger(EquivalentInput.Text,"equivalent focal length"),
            Iso=OptionalInteger(IsoInput.Text,"ISO"),ExposureSeconds=shutter,ExposureBias=OptionalNumber(ExposureInput.Text,"exposure compensation"),
            Flash=(FlashPicker.SelectedItem as CameraOption)?.Value,WhiteBalance=(WhiteBalancePicker.SelectedItem as CameraOption)?.Value,UseExportDateWhenMissing=DateFallback.IsChecked==true};
    }
    private void ApplyCameraSettings(ExportSettings s)
    {
        SetCameraChoices(s.Camera);
        LensMakeInput.Text=s.LensMake;LensModelInput.Text=s.LensModel;
        ApertureInput.Text=s.FNumber?.ToString(Invariant)??"";PhysicalInput.Text=s.FocalLength?.ToString(Invariant)??"";
        EquivalentInput.Text=s.FocalLength35mm?.ToString(Invariant)??"";IsoInput.Text=s.Iso?.ToString(Invariant)??"";
        ShutterInput.Text=s.ExposureSeconds?.ToString("G",Invariant)??"";ExposureInput.Text=s.ExposureBias?.ToString(Invariant)??"";
        FlashPicker.SelectedItem=FlashPicker.Items.Cast<CameraOption>().FirstOrDefault(o=>o.Value==s.Flash);
        WhiteBalancePicker.SelectedItem=WhiteBalancePicker.Items.Cast<CameraOption>().FirstOrDefault(o=>o.Value==s.WhiteBalance);
    }
    private async Task RunCameraUiChecks()
    {
        if(ApertureInput.Text!="1.78"||EquivalentInput.Text!="24")throw new InvalidOperationException("Default main camera was not filled.");
        if(IsoInput.Text!="100"||SoftwareInput.Text!="18.1"||ShutterInput.Text!="0.004"||DateFallback.IsChecked!=true||PhysicalInput.Text!="6.765")throw new InvalidOperationException("Complete default profile was not filled.");
        CameraPicker.SelectedItem="Ultra Wide";
        if(ApertureInput.Text!="2.2"||EquivalentInput.Text!="13")throw new InvalidOperationException("Camera selection did not autofill.");
        var settings=new ExportSettings{Model="iPhone 15 Pro",Camera="Main",LensMake="Custom manufacturer",LensModel="Custom lens",FNumber=2.1,FocalLength=7.25,FocalLength35mm=28,Iso=200,ExposureSeconds=0.002,ExposureBias=-0.7,Flash=25,WhiteBalance=1,Software="18.2",KeepOriginalDate=false,CaptureDate=new DateTimeOffset(2025,2,3,4,5,6,TimeSpan.FromHours(-5)),Latitude=-23.5,Longitude=45.6,IphoneNames=false,StartNumber=37,PreserveFolders=true,AllowExtraTrackRemoval=true,OutputDirectory=OutputFolder.Text};
        var preset=new SavedPreset("Diagnostic",settings);
        SavedPresetPicker.ItemsSource=new[]{preset};SavedPresetPicker.SelectedItem=preset;
        await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
        if(ReadSettings()!=settings)throw new InvalidOperationException("Saved preset restoration changed settings.");
        ShutterInput.Text="1/320";
        if(ReadSettings().ExposureSeconds!=1d/320)throw new InvalidOperationException("Fraction shutter input failed.");
        ModelPicker.Text="iPhone 16";ScheduleCameraModelChange();
        await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
        if(ApertureInput.Text!="1.6"||EquivalentInput.Text!="26")throw new InvalidOperationException("Model switch did not update hardware.");
        if(ReadSettings().Iso!=100||ReadSettings().ExposureSeconds!=1d/250||ReadSettings().WhiteBalance!=0||ReadSettings().Flash!=16||!ReadSettings().UseExportDateWhenMissing)throw new InvalidOperationException("Model switch did not fill complete defaults.");
        var old=new SavedPreset("Old",new ExportSettings{Model="iPhone 14 Pro",OutputDirectory=OutputFolder.Text});
        SavedPresetPicker.ItemsSource=new[]{old};SavedPresetPicker.SelectedItem=old;
        await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
        if(LensModelInput.Text!=""||ApertureInput.Text!="")throw new InvalidOperationException("Old preset invented camera fields.");
        ModelPicker.Text="iPhone 16e";ScheduleCameraModelChange();
        await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
        if(CameraPicker.SelectedItem?.ToString()!="Main"||ReadSettings().Iso!=100||ReadSettings().Software!="18.3"||ReadSettings().FocalLength!=4.2)throw new InvalidOperationException("Old preset blocked automatic phone defaults.");
    }
}
