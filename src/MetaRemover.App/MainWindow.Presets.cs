using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using MetaRemover.Core;
namespace MetaRemover.App;
public partial class MainWindow
{
    private readonly SavedPresetStore presetStore=new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MetaRemover","presets.json"));
    private void LoadSavedPresets(string? select=null)
    {
        try
        {
            var presets=presetStore.Load();
            SavedPresetPicker.ItemsSource=presets;
            if(select!=null)SavedPresetPicker.SelectedItem=presets.FirstOrDefault(p=>string.Equals(p.Name,select.Trim(),StringComparison.OrdinalIgnoreCase));
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {ShowError("Could not load saved presets: "+e.Message);}
    }
    private void SavePreset_Click(object sender,RoutedEventArgs e)
    {
        try{presetStore.Save(PresetName.Text,ReadSettings());LoadSavedPresets(PresetName.Text);}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or FormatException or OverflowException)
        {ShowError("Could not save preset: "+ex.Message);}
    }
    private void DeletePreset_Click(object sender,RoutedEventArgs e)
    {
        if(SavedPresetPicker.SelectedItem is not SavedPreset p){ShowError("Select a saved preset to delete.");return;}
        try{presetStore.Delete(p.Name);LoadSavedPresets();PresetName.Text="";}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {ShowError("Could not delete preset: "+ex.Message);}
    }
    private void SavedPreset_Changed(object sender,SelectionChangedEventArgs e)
    {
        if(!ready||SavedPresetPicker.SelectedItem is not SavedPreset p)return;
        ready=false;
        try
        {
            var s=p.Settings;
            PresetName.Text=p.Name;
            RemoveMode.IsChecked=s.RemoveOnly;ReplaceMode.IsChecked=!s.RemoveOnly;
            ModelPicker.Text=s.Model;SoftwareInput.Text=s.Software;
            ApplyCameraSettings(s);
            KeepDate.IsChecked=s.KeepOriginalDate;
            DateFallback.IsChecked=s.UseExportDateWhenMissing;
            if(s.CaptureDate is DateTimeOffset date)
            {
                DateInput.Text=date.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);
                OffsetPicker.SelectedItem=date.ToString("zzz",CultureInfo.InvariantCulture);
            }
            GpsEnabled.IsChecked=s.Latitude.HasValue;
            Latitude.Text=s.Latitude?.ToString(CultureInfo.InvariantCulture)??"";
            Longitude.Text=s.Longitude?.ToString(CultureInfo.InvariantCulture)??"";
            IphoneNames.IsChecked=s.IphoneNames;StartNumber.Text=s.StartNumber.ToString("D4");
            PreserveFolders.IsChecked=s.PreserveFolders;DropExtraTracks.IsChecked=s.AllowExtraTrackRemoval;
            DateInput.IsEnabled=!s.KeepOriginalDate;OffsetPicker.IsEnabled=!s.KeepOriginalDate;
            GpsFields.Visibility=s.Latitude.HasValue?Visibility.Visible:Visibility.Collapsed;
        }
        finally{ready=true;}
        Model.SetPreset(RemoveMode.IsChecked==true?"Remove metadata":ModelPicker.Text);
        RefreshPreview();
    }
}
