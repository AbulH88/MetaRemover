namespace MetaRemover.Core;
public sealed class ImportService
{
    public static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    public static readonly string[] VideoExtensions = [".mov", ".mp4"];
    public ImportResult Import(IEnumerable<string> paths, bool recursive, string? excludedDirectory)
    {
        var files = new List<MediaFile>();
        var errors = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? excluded = string.IsNullOrWhiteSpace(excludedDirectory) ? null : System.IO.Path.GetFullPath(excludedDirectory).TrimEnd(System.IO.Path.DirectorySeparatorChar);
        bool Excluded(string path) => excluded != null && (path.Equals(excluded, StringComparison.OrdinalIgnoreCase) || path.StartsWith(excluded + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        void Add(string path, string? root)
        {
            if (Excluded(path) || !seen.Add(path)) return;
            var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
            MediaKind kind;
            if (ImageExtensions.Contains(extension)) kind = MediaKind.Image;
            else if (VideoExtensions.Contains(extension)) kind = MediaKind.Video;
            else { errors.Add("Unsupported format: " + path); return; }
            var info = new FileInfo(path);
            files.Add(new(path, kind, info.Length) { RelativeFolder = root == null ? "" : System.IO.Path.GetDirectoryName(System.IO.Path.GetRelativePath(root, path)) ?? "" });
        }
        foreach (var input in paths)
        {
            try
            {
                var path = System.IO.Path.GetFullPath(input);
                if (Excluded(path)) continue;
                if (File.Exists(path)) { Add(path, null); continue; }
                if (!Directory.Exists(path)) { errors.Add("Not found: " + path); continue; }
                var pending = new Stack<string>(); pending.Push(path);
                while (pending.TryPop(out var folder))
                {
                    if (Excluded(folder)) continue;
                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(folder).Order(StringComparer.OrdinalIgnoreCase))
                            try { Add(file, path); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { errors.Add(file + ": " + e.Message); }
                        if (recursive)
                            foreach (var child in Directory.EnumerateDirectories(folder).OrderDescending(StringComparer.OrdinalIgnoreCase))
                                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) pending.Push(child);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException) { errors.Add(folder + ": " + e.Message); }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { errors.Add(input + ": " + e.Message); }
        }
        return new(files, errors);
    }
}
