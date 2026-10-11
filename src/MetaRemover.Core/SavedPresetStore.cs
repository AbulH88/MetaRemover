using System.Text.Json;
namespace MetaRemover.Core;
public record SavedPreset(string Name, ExportSettings Settings);
public sealed class SavedPresetStore(string path)
{
    public IReadOnlyList<SavedPreset> Load()
    {
        if (!File.Exists(path)) return [];
        var entries = JsonSerializer.Deserialize<List<SavedPreset>>(File.ReadAllText(path))
            ?? throw new JsonException("Preset storage is empty or invalid.");
        foreach (var entry in entries)
        {
            if (entry is null || entry.Settings is null || string.IsNullOrWhiteSpace(entry.Name))
                throw new JsonException("Invalid saved preset.");
            Validate(entry.Name,entry.Settings);
        }
        if(entries.Select(p=>p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=entries.Count)
            throw new JsonException("Duplicate saved preset names.");
        return entries.OrderBy(p=>p.Name,StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public void Save(string name, ExportSettings settings)
    {
        Validate(name,settings);
        var entries=Load().ToList();
        entries.RemoveAll(p=>string.Equals(p.Name,name.Trim(),StringComparison.OrdinalIgnoreCase));
        entries.Add(new(name.Trim(),settings with {OutputDirectory=""}));
        Write(entries);
    }
    public void Delete(string name)
    {
        var entries=Load().ToList();
        entries.RemoveAll(p=>string.Equals(p.Name,name,StringComparison.OrdinalIgnoreCase));
        Write(entries);
    }
    private static void Validate(string name,ExportSettings settings)
    {
        if(settings is null || settings.Model is null || settings.Software is null)
            throw new ArgumentException("Preset model and software fields must contain text.");
        if(string.IsNullOrWhiteSpace(name)||name.Length>80||name.Any(char.IsControl))
            throw new ArgumentException("Enter a preset name (1–80 characters).");
        var errors=new PresetService().Validate(settings);
        if(!settings.RemoveOnly&&!settings.KeepOriginalDate&&settings.CaptureDate is null)
            throw new ArgumentException("Enter a capture date or choose Keep original.");
        if(errors.Count>0)throw new ArgumentException(string.Join("\n",errors));
    }
    private void Write(List<SavedPreset> entries)
    {
        var full=Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temporary=full+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            File.WriteAllText(temporary,JsonSerializer.Serialize(entries,new JsonSerializerOptions{WriteIndented=true}));
            File.Move(temporary,full,true);
        }
        finally {if(File.Exists(temporary))File.Delete(temporary);}
    }
}
