using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MetaRemover.Core;

namespace MetaRemover.App;

public record PreviewRow(string Label,string Value);
public partial class MainWindow : Window
{
    public WorkspaceModel Model { get; }
    private bool ready;
    private QueueItem? selected;
    private MediaInspection? inspection;
    private CancellationTokenSource? inspectCancel;
    private readonly CancellationTokenSource lifetime=new();
    private readonly string cache=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MetaRemover","Cache",Guid.NewGuid().ToString("N"));
    public MainWindow()
    {
        Model=new(ToolPaths.Bundled(AppContext.BaseDirectory));
        InitializeComponent(); DataContext=Model;
        ModelPicker.ItemsSource=PresetService.Models;ModelPicker.Text="iPhone 16 Pro";
        OffsetPicker.ItemsSource=Enumerable.Range(-48,105).Select(n=>TimeSpan.FromMinutes(n*15).ToString(@"hh\:mm").Insert(0,n<0?"-":"+")).ToArray();
        OffsetPicker.SelectedItem="+06:00";
        DateInput.Text=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(6)).ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);
        OutputFolder.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),"MetaRemover");
        DateInput.IsEnabled=false;OffsetPicker.IsEnabled=false;
        InitializeCameraControls();
        Directory.CreateDirectory(cache);
        ready=true;
        LoadSavedPresets();
        Model.Files.CollectionChanged+=(_,_)=>EmptyState.Visibility=Model.Files.Count==0?Visibility.Visible:Visibility.Collapsed;
        Loaded+=Window_Loaded;
        Closing+=(_,e)=> { if(Model.IsBusy){Model.Cancel();e.Cancel=true;MessageBox.Show(this,"The active operation is canceling. Close the window after it stops.","MetaRemover");} };
        Closed+=(_,_)=> {lifetime.Cancel();inspectCancel?.Cancel();try{foreach(var path in Directory.GetFiles(cache))File.Delete(path);Directory.Delete(cache);}catch(IOException){} lifetime.Dispose();};
    }
    private async void Window_Loaded(object sender,RoutedEventArgs e)
    {
        var args=Environment.GetCommandLineArgs();
        var cameraCheck=Array.IndexOf(args,"--camera-ui-test");
        if(cameraCheck>=0&&cameraCheck+1<args.Length)
        {
            try{await RunCameraUiChecks();File.WriteAllText(args[cameraCheck+1],"Camera defaults, saved preset restoration and model switching: PASS");}
            catch(Exception ex){File.WriteAllText(args[cameraCheck+1],ex.ToString());Environment.ExitCode=1;}
            Close();return;
        }
        var index=Array.IndexOf(args,"--demo-folder");
        if(index>=0&&index+1<args.Length)await ImportAsync([args[index+1]]);
        var shot=Array.IndexOf(args,"--screenshot");
        if(shot>=0&&shot+1<args.Length)
        {
            await Task.Delay(1200);
            UpdateLayout();
            var visual=(FrameworkElement)Content;
            var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)visual.ActualWidth,(int)visual.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using(var stream=File.Create(args[shot+1]))encoder.Save(stream);
            Close();
        }
    }
    private async Task ImportAsync(IEnumerable<string> paths)
    {
        try
        {
            await Model.ImportAsync(paths,IncludeSubfolders.IsChecked==true,OutputFolder.Text);
            Model.SetPreset(RemoveMode.IsChecked==true?"Remove metadata":ModelPicker.Text);
            var pending=Model.Files.Where(i=>i.ThumbnailPath==null).ToArray();
            _=LoadThumbnailsAsync(pending);
            if(FileGrid.SelectedItem==null&&Model.Files.Count>0)FileGrid.SelectedItem=Model.Files[0];
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException){ShowError(e.Message);}
    }
    private async Task LoadThumbnailsAsync(IEnumerable<QueueItem> items)
    {
        foreach(var item in items)
        {
            if(lifetime.IsCancellationRequested)return;
            try
            {
                var path=Path.Combine(cache,Guid.NewGuid().ToString("N")+".png");
                await new ToolRunner().RunAsync(Model.Tools.Ffmpeg,["-v","error","-nostdin","-i",item.File.Path,"-frames:v","1","-vf","scale=180:100:force_original_aspect_ratio=increase,crop=180:100","-threads","1","-update","1","-y",path],lifetime.Token);
                item.ThumbnailPath=path;
                if(item.IsVideo)
                {
                    var data=await new MetadataReader(Model.Tools).ReadAsync(item.File.Path,lifetime.Token);
                    item.Duration=TimeSpan.FromSeconds(data.Duration).ToString(data.Duration>=3600?@"hh\:mm\:ss":@"mm\:ss");
                }
                if(selected==item)PreviewImage.Source=(BitmapSource?)new ThumbnailConverter().Convert(path,typeof(BitmapSource),null!,CultureInfo.InvariantCulture);
            }
            catch(OperationCanceledException){return;}
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException){item.Detail="Preview unavailable: "+e.Message;}
        }
    }
    private async void AddFiles_Click(object sender,RoutedEventArgs e)
    {
        var dialog=new OpenFileDialog{Multiselect=true,Title="Import images and videos",Filter="Supported media|*.jpg;*.jpeg;*.png;*.webp;*.mp4;*.mov|All files|*.*"};
        if(dialog.ShowDialog(this)==true)await ImportAsync(dialog.FileNames);
    }
    private async void AddFolder_Click(object sender,RoutedEventArgs e)
    {
        var dialog=new OpenFolderDialog{Title="Import a folder"};
        if(dialog.ShowDialog(this)==true)await ImportAsync([dialog.FolderName]);
    }
    private async void Window_Drop(object sender,DragEventArgs e)
    {
        if(Model.IsBusy)return;
        if(e.Data.GetData(DataFormats.FileDrop) is string[] paths)await ImportAsync(paths);
    }
    private void Window_DragOver(object sender,DragEventArgs e){e.Effects=!Model.IsBusy&&e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;}
    private void Remove_Click(object sender,RoutedEventArgs e){Model.RemoveSelected();if(Model.Files.Count==0){selected=null;inspection=null;BeforeList.ItemsSource=null;AfterList.ItemsSource=null;PreviewName.Text="Select a file to inspect";PreviewImage.Source=null;}}
    private void SelectAll_Click(object sender,RoutedEventArgs e)=>Model.SelectAll();
    private void SelectNone_Click(object sender,RoutedEventArgs e)=>Model.SelectNone();
    private void HeaderSelect_Click(object sender,RoutedEventArgs e){if(HeaderCheck.IsChecked==true)Model.SelectAll();else Model.SelectNone();}
    private void BrowseOutput_Click(object sender,RoutedEventArgs e)
    {
        var dialog=new OpenFolderDialog{Title="Choose export folder"};
        if(Directory.Exists(OutputFolder.Text))dialog.InitialDirectory=OutputFolder.Text;
        if(dialog.ShowDialog(this)==true)OutputFolder.Text=dialog.FolderName;
    }
    private void ImportReport_Click(object sender,RoutedEventArgs e)=>MessageBox.Show(this,Model.LastImportErrors.Count==0?"No skipped files in the last import.":string.Join("\n",Model.LastImportErrors.Take(50)),"Import report");
    private void OpenOutput_Click(object sender,RoutedEventArgs e)
    {
        try
        {
            if(!Directory.Exists(OutputFolder.Text)){ShowError("The output folder does not exist yet.");return;}
            Process.Start(new ProcessStartInfo("explorer.exe"){ArgumentList={Path.GetFullPath(OutputFolder.Text)},UseShellExecute=false});
        }
        catch(Exception ex) when(ex is IOException or System.ComponentModel.Win32Exception or ArgumentException){ShowError(ex.Message);}
    }
    private void Cancel_Click(object sender,RoutedEventArgs e)=>Model.Cancel();
    private async void ExportSelected_Click(object sender,RoutedEventArgs e)=>await ExportAsync(true);
    private async void ExportAll_Click(object sender,RoutedEventArgs e)=>await ExportAsync(false);
    private async Task ExportAsync(bool selectedOnly)
    {
        try
        {
            var settings=ReadSettings();
            await Model.ExportAsync(selectedOnly,settings);
            if(selected!=null)await RefreshInspectionAsync(selected);
            if(Model.Failed>0)MessageBox.Show(this,$"{Model.Completed} exported; {Model.Failed} failed. Select a failed row to read its reason. Originals are preserved.","Export results");
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or FormatException or OverflowException){ShowError(e.Message);}
    }
    private ExportSettings ReadSettings()
    {
        var remove=RemoveMode.IsChecked==true;
        DateTimeOffset? date=null;double? lat=null,lon=null;
        if(!remove&&KeepDate.IsChecked!=true)
        {
            if(!DateTime.TryParseExact(DateInput.Text.Trim(),["yyyy-MM-dd HH:mm:ss","yyyy-MM-dd HH:mm"],CultureInfo.InvariantCulture,DateTimeStyles.None,out var time))throw new ArgumentException("Date format: yyyy-MM-dd HH:mm:ss");
            var offset=OffsetPicker.SelectedItem?.ToString()??"+06:00";
            var span=TimeSpan.ParseExact(offset[1..],@"hh\:mm",CultureInfo.InvariantCulture);
            if(offset.StartsWith('-'))span=-span;
            date=new(DateTime.SpecifyKind(time,DateTimeKind.Unspecified),span);
        }
        if(!remove&&GpsEnabled.IsChecked==true)
        {
            if(!double.TryParse(Latitude.Text,CultureInfo.InvariantCulture,out var latitude)||!double.TryParse(Longitude.Text,CultureInfo.InvariantCulture,out var longitude))throw new ArgumentException("Enter numeric latitude and longitude.");
            lat=latitude;lon=longitude;
        }
        if(!int.TryParse(StartNumber.Text,out var start))throw new ArgumentException("Enter a starting number from 0001 to 9999.");
        var settings=new ExportSettings{RemoveOnly=remove,Model=ModelPicker.Text,Software=SoftwareInput.Text,CaptureDate=date,
            KeepOriginalDate=KeepDate.IsChecked==true,Latitude=lat,Longitude=lon,OutputDirectory=OutputFolder.Text,
            StartNumber=start,IphoneNames=IphoneNames.IsChecked==true,PreserveFolders=PreserveFolders.IsChecked==true,AllowExtraTrackRemoval=DropExtraTracks.IsChecked==true};
        settings=ReadCameraSettings(settings);
        var errors=new PresetService().Validate(settings);if(errors.Count>0)throw new ArgumentException(string.Join("\n",errors));
        return settings;
    }
    private async void FileGrid_SelectionChanged(object sender,SelectionChangedEventArgs e)
    {
        if(!ready)return;
        if(FileGrid.SelectedItem is QueueItem item)await RefreshInspectionAsync(item);
    }
    private async Task RefreshInspectionAsync(QueueItem item)
    {
        inspectCancel?.Cancel();inspectCancel?.Dispose();inspectCancel=new();
        var token=inspectCancel.Token;selected=item;inspection=null;
        PreviewName.Text=item.Name;PreviewInfo.Text=item.Type+" · "+item.Size;
        PreviewImage.Source=(BitmapSource?)new ThumbnailConverter().Convert(item.ThumbnailPath!,typeof(BitmapSource),null!,CultureInfo.InvariantCulture);
        BeforeList.ItemsSource=null;AfterList.ItemsSource=null;PreviewNotice.Text="Reading metadata…";
        try
        {
            inspection=await new MetadataReader(Model.Tools).ReadAsync(item.File.Path,token);
            if(token.IsCancellationRequested)return;
            BeforeList.ItemsSource=Rows(inspection);
            if(item.Result?.Success==true)
            {
                var actual=await new MetadataReader(Model.Tools).ReadAsync(item.Result.Path!,token);
                if(token.IsCancellationRequested)return;
                AfterHeading.Text="After (verified output)";AfterList.ItemsSource=Rows(actual);
                PreviewNotice.Text=item.Detail;
            }
            else RefreshPreview();
        }
        catch(OperationCanceledException){}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { if(!token.IsCancellationRequested)PreviewNotice.Text=e.Message; }
    }
    private static List<PreviewRow> Rows(MediaInspection i)=>[
        new("Camera Make",i.Get("Make")??"—"),new("Camera Model",i.Get("Model")??"—"),
        new("Date Taken",i.Get("DateTimeOriginal")??i.Get("CreationDate")??i.Get("CreateDate")??"—"),
        new("GPS Location",i.Get("GPSPosition")??i.Get("GPSCoordinates")??(i.Get("GPSLatitude") is string lat?lat+", "+i.Get("GPSLongitude"):"Not set")),
        new("Dimensions",$"{i.Width} × {i.Height}"),new("Software",i.Get("Software")??"—"),
        new("Lens Make",i.Get("LensMake")??"—"),new("Lens Model",i.Get("LensModel")??"—"),
        new("Aperture",i.Get("FNumber") is string aperture?"f/"+aperture:"—"),
        new("Focal Length",i.Get("FocalLength") is string focal?focal+" mm":"—"),
        new("35mm Equiv.",i.Get("FocalLengthIn35mmFormat") is string equiv?equiv+" mm":"—"),
        new("ISO",i.Get("ISO")??"—"),new("Shutter",i.Get("ExposureTime") is string exposure?exposure+" s":"—"),
        new("Exposure EV",i.Get("ExposureCompensation")??"—"),new("Flash",FlashLabel(i.Get("Flash"))),new("White Balance",BalanceLabel(i.Get("WhiteBalance"))),
        new("Other",i.Tags.Count+" readable fields")];
    private void Settings_Changed(object sender,RoutedEventArgs e)
    {
        if(!ready)return;
        ScheduleCameraModelChange();
        Model.SetPreset(RemoveMode.IsChecked==true?"Remove metadata":ModelPicker.Text);
        RefreshPreview();
    }
    private void DatePolicy_Changed(object sender,RoutedEventArgs e)
    {
        if(!ready)return;
        DateInput.IsEnabled=KeepDate.IsChecked!=true;OffsetPicker.IsEnabled=KeepDate.IsChecked!=true;RefreshPreview();
    }
    private void Gps_Changed(object sender,RoutedEventArgs e)
    {
        if(!ready)return;
        GpsFields.Visibility=GpsEnabled.IsChecked==true?Visibility.Visible:Visibility.Collapsed;RefreshPreview();
    }
    private void RefreshPreview()
    {
        if(!ready)return;
        var ext=selected?.Type??"JPG";
        FilenamePreview.Text=IphoneNames.IsChecked==true?"IMG_"+(int.TryParse(StartNumber.Text,out var n)?n:1).ToString("D4")+"."+ext:"Original filename";
        ModelPicker.IsEnabled=RemoveMode.IsChecked!=true;
        if(inspection==null)return;
        AfterHeading.Text="After (planned metadata)";
        try
        {
            var s=ReadSettings();
            var planned=new List<PreviewRow>{
                new("Camera Make",s.RemoveOnly?"—":"Apple"),new("Camera Model",s.RemoveOnly?"—":s.Model),
                new("Date Taken",s.RemoveOnly?"Removed":s.KeepOriginalDate?CaptureDateService.HasOriginal(inspection,selected?.File.Kind??MediaKind.Image)?inspection.Get("DateTimeOriginal")??inspection.Get("CreationDate")??inspection.Get("CreateDate")??"—":s.UseExportDateWhenMissing?"Export time (automatic)":"—":s.CaptureDate?.ToString("yyyy-MM-dd HH:mm:ss zzz")??"—"),
                new("GPS Location",s.Latitude.HasValue?$"{s.Latitude}, {s.Longitude}":"Not set"),
                new("Dimensions",$"{inspection.Width} × {inspection.Height}"),new("Software",s.RemoveOnly?"—":string.IsNullOrWhiteSpace(s.Software)?"—":s.Software),new("Other","Rendering / playback fields")};
            var tags=new PresetService().BuildTags(selected?.File.Kind??MediaKind.Image,s);
            string? Get(string name)=>tags.FirstOrDefault(k=>k.Key.EndsWith(":"+name)||k.Key.EndsWith(":"+name+"#")).Value;
            planned.InsertRange(planned.Count-1,new PreviewRow[]{
                new("Lens Make",Get("LensMake")??"—"),new("Lens Model",Get("LensModel")??"—"),
                new("Aperture",Get("FNumber") is string f?"f/"+f:"—"),new("Focal Length",Get("FocalLength") is string fl?fl+" mm":"—"),
                new("35mm Equiv.",Get("FocalLengthIn35mmFormat") is string eq?eq+" mm":"—"),new("ISO",Get("ISO")??"—"),
                new("Shutter",Get("ExposureTime") is string t?t+" s":"—"),new("Exposure EV",Get("ExposureCompensation")??"—"),
                new("Flash",FlashLabel(Get("Flash"))),new("White Balance",BalanceLabel(Get("WhiteBalance")))});
            AfterList.ItemsSource=planned;
            PreviewNotice.Text=inspection.HasExtraTracks?"Extra tracks detected. Export requires explicit removal under More options.":"Orientation, color profiles and required playback fields are preserved.";
            if(selected?.Result?.Success==false)PreviewNotice.Text=selected.Result.Error??PreviewNotice.Text;
        }
        catch(Exception e) when(e is ArgumentException or FormatException or OverflowException){PreviewNotice.Text=e.Message;}
    }
    private void ShowError(string message)=>MessageBox.Show(this,message,"MetaRemover",MessageBoxButton.OK,MessageBoxImage.Information);
    private static string FlashLabel(string? value)=>value switch{"0"=>"Did not fire","1"=>"Fired","16"=>"Off","24"=>"Auto, did not fire","25"=>"Auto, fired",null=>"—",_=>value};
    private static string BalanceLabel(string? value)=>value switch{"0"=>"Auto","1"=>"Manual",null=>"—",_=>value};
}
