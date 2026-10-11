namespace MetaRemover.Core;
public sealed class OutputAllocator
{
    private readonly string directory;
    private readonly bool iphoneNames;
    private int counter;
    private int batch = 1;
    public OutputAllocator(string directory, bool iphoneNames, int start)
    {
        if (start is < 1 or > 9999) throw new ArgumentOutOfRangeException(nameof(start));
        this.directory = Path.GetFullPath(directory); this.iphoneNames = iphoneNames; counter = start;
        Directory.CreateDirectory(this.directory);
    }
    public OutputReservation Reserve(string source, string? relativeFolder = null)
    {
        var folder = directory;
        if (!string.IsNullOrEmpty(relativeFolder))
        {
            folder = Path.GetFullPath(Path.Combine(directory, relativeFolder));
            if (!folder.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Output folder must stay inside the destination.");
        }
        var suffix = 0;
        while (true)
        {
            if (counter > 9999) { counter = 1; batch++; }
            var targetFolder = batch == 1 ? folder : Path.Combine(folder, $"Export_{batch:D3}");
            Directory.CreateDirectory(targetFolder);
            var name = iphoneNames ? $"IMG_{counter++:D4}{Path.GetExtension(source).ToUpperInvariant()}" : Path.GetFileNameWithoutExtension(source) + (suffix++ == 0 ? "" : $"_{suffix - 1}") + Path.GetExtension(source);
            var final = Path.Combine(targetFolder, name);
            if (File.Exists(final) || Directory.Exists(final)) continue;
            var lockPath = final + ".metaremover-reserve";
            FileStream lease;
            try { lease = new(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None); }
            catch (IOException) when (File.Exists(lockPath)) { continue; }
            if (File.Exists(final)) { lease.Dispose(); File.Delete(lockPath); continue; }
            return new(final, Path.Combine(targetFolder, ".metaremover-" + Guid.NewGuid().ToString("N") + Path.GetExtension(source)), lockPath, lease);
        }
    }
}
public sealed class OutputReservation : IDisposable
{
    public string FinalPath { get; }
    public string StagingPath { get; }
    private readonly string lockPath;
    private readonly FileStream lease;
    public OutputReservation(string finalPath, string stagingPath, string lockPath, FileStream lease) => (FinalPath, StagingPath, this.lockPath, this.lease) = (finalPath, stagingPath, lockPath, lease);
    public void Publish() => File.Move(StagingPath, FinalPath, false);
    public void Dispose()
    {
        lease.Dispose();
        if (File.Exists(lockPath)) File.Delete(lockPath);
        if (File.Exists(StagingPath)) File.Delete(StagingPath);
        var exifToolSidecar = StagingPath + "_exiftool_tmp";
        if (File.Exists(exifToolSidecar)) File.Delete(exifToolSidecar);
    }
}
