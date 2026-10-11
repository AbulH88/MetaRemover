using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MetaRemover.Core;

public class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this,new(name));
    protected bool Set<T>(ref T field,T value,[CallerMemberName] string? name = null)
    {
        if(EqualityComparer<T>.Default.Equals(field,value)) return false;
        field=value; Notify(name); return true;
    }
}
public class QueueItem(MediaFile file) : Observable
{
    public MediaFile File { get; }=file;
    public string Name => Path.GetFileName(File.Path);
    public string SourcePath => File.Path;
    public string Type => Path.GetExtension(File.Path).TrimStart('.').ToUpperInvariant();
    public string Size => File.Bytes >= 1073741824 ? $"{File.Bytes/1073741824d:0.0} GB" : File.Bytes >= 1048576 ? $"{File.Bytes/1048576d:0.0} MB" : File.Bytes >= 1024 ? $"{File.Bytes/1024d:0.0} KB" : $"{File.Bytes} B";
    public bool IsVideo => File.Kind==MediaKind.Video;
    private bool isChecked=true;
    public bool IsChecked { get=>isChecked; set=>Set(ref isChecked,value); }
    private string status="Ready";
    public string Status { get=>status; set=>Set(ref status,value); }
    private string preset="iPhone 16 Pro";
    public string Preset { get=>preset; set=>Set(ref preset,value); }
    private string detail="";
    public string Detail { get=>detail; set=>Set(ref detail,value); }
    private string? thumbnailPath;
    public string? ThumbnailPath { get=>thumbnailPath; set=>Set(ref thumbnailPath,value); }
    private string duration="";
    public string Duration { get=>duration; set=>Set(ref duration,value); }
    public ExportResult? Result { get; set; }
}
public class WorkspaceModel(ToolPaths paths) : Observable
{
    public ObservableCollection<QueueItem> Files { get; }=[];
    public ToolPaths Tools { get; }=paths;
    private bool busy;
    public bool IsBusy { get=>busy; private set { if(Set(ref busy,value)) Notify(nameof(IsIdle)); } }
    public bool IsIdle=>!IsBusy;
    private int completed;
    public int Completed { get=>completed; private set=>Set(ref completed,value); }
    private int failed;
    public int Failed { get=>failed; private set=>Set(ref failed,value); }
    private double progress;
    public double Progress { get=>progress; private set=>Set(ref progress,value); }
    private string summary="Ready to import";
    public string Summary { get=>summary; private set=>Set(ref summary,value); }
    private CancellationTokenSource? cancellation;
    public string SelectionSummary=>$"{Files.Count(f=>f.IsChecked)} of {Files.Count} files selected";
    public string SelectedButtonText=>$"Export selected ({Files.Count(f=>f.IsChecked)})";
    public string AllButtonText=>$"Export all ({Files.Count})";
    public void UpdateSelection()
    {
        Notify(nameof(SelectionSummary)); Notify(nameof(SelectedButtonText)); Notify(nameof(AllButtonText));
    }
    public async Task ImportAsync(IEnumerable<string> input,bool recursive,string? excludedDirectory)
    {
        if(IsBusy) return;
        IsBusy=true;
        try
        {
            Summary="Importing files…";
            var result=await Task.Run(()=>new ImportService().Import(input,recursive,excludedDirectory));
            var seen=Files.Select(f=>f.File.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach(var file in result.Files)
            {
                if(!seen.Add(file.Path)) continue;
                var item=new QueueItem(file);
                item.PropertyChanged+=(_,e)=> { if(e.PropertyName==nameof(QueueItem.IsChecked)) UpdateSelection(); };
                Files.Add(item);
            }
            LastImportErrors=result.Errors;
            Summary=result.Errors.Count>0?$"Imported {result.Files.Count} files · {result.Errors.Count} skipped — see import report":$"{Files.Count} files ready";
            UpdateSelection();
        }
        finally {IsBusy=false;}
    }
    public IReadOnlyList<string> LastImportErrors {get;private set;}=[];
    public Task<MediaInspection> InspectAsync(QueueItem item,CancellationToken token)=>new MetadataReader(Tools).ReadAsync(item.Result?.Path??item.File.Path,token);
    public void SelectNone(){if(IsBusy)return;foreach(var item in Files)item.IsChecked=false;UpdateSelection();}
    public void SelectAll(){if(IsBusy)return;foreach(var item in Files)item.IsChecked=true;UpdateSelection();}
    public void RemoveSelected(){if(IsBusy)return;foreach(var item in Files.Where(f=>f.IsChecked).ToArray())Files.Remove(item);UpdateSelection();}
    public void SetPreset(string preset){foreach(var item in Files)item.Preset=preset;}
    public void Cancel()=>cancellation?.Cancel();
    public async Task ExportAsync(bool selectedOnly,ExportSettings settings)
    {
        if(IsBusy)return;
        var snapshot=Files.Where(f=>!selectedOnly||f.IsChecked).ToArray();
        if(snapshot.Length==0){Summary="Select or import files first.";return;}
        if(string.IsNullOrWhiteSpace(settings.OutputDirectory))throw new ArgumentException("Choose an output folder.");
        var validation=new PresetService().Validate(settings);
        if(validation.Count>0)throw new ArgumentException(string.Join(" ",validation));
        // A source file can never be used as the output because the allocator skips existing paths.
        if(settings.UseExportDateWhenMissing&&settings.FallbackCaptureDate is null)settings=settings with {FallbackCaptureDate=DateTimeOffset.Now};
        IsBusy=true;Completed=0;Failed=0;Progress=0;
        using var cts=new CancellationTokenSource();cancellation=cts;
        QueueItem? active=null;
        try
        {
            var allocator=new OutputAllocator(settings.OutputDirectory,settings.IphoneNames,settings.StartNumber);
            foreach(var item in snapshot)
            {
                cts.Token.ThrowIfCancellationRequested(); active=item;
                item.Status="Processing"; item.Detail="";
                Summary=$"Processing {Completed+Failed+1} of {snapshot.Length} · {item.Name}";
                var result=await new ProcessingService(Tools).ExportAsync(item.File,settings,allocator,cts.Token);
                item.Result=result;
                item.Status=!result.Success?"Failed":result.Warnings.Count>0?"Review":"Complete";
                item.Detail=result.Success?result.Path+ (result.Warnings.Count>0?"\n"+string.Join("\n",result.Warnings):""):result.Error??"Unknown failure";
                item.Preset=settings.RemoveOnly?"Remove metadata":settings.Model;
                if(result.Success)Completed++;else Failed++;
                Progress=(Completed+Failed)*100d/snapshot.Length;
            }
            Summary=$"Finished · {Completed} exported · {Failed} failed · {snapshot.Count(i=>i.Status=="Review")} to review";
        }
        catch(OperationCanceledException)
        {
            if(active?.Status=="Processing"){active.Status="Canceled";active.Detail="Incomplete output removed. Original preserved.";}
            Summary=$"Canceled · {Completed} completed exports kept";
        }
        finally { cancellation=null; IsBusy=false; }
    }
}
