using System.Diagnostics;
using System.Text;

namespace MetaRemover.Core;

public record ToolResult(int ExitCode, string Output, string Error);
public sealed class ToolRunner
{
    public async Task<ToolResult> RunAsync(string executable, IReadOnlyList<string> args, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (args.Any(a => a.Any(char.IsControl))) throw new ArgumentException("Metadata and file arguments must not contain control characters.");
        if (!File.Exists(executable)) throw new FileNotFoundException("A bundled processing tool is missing. Re-extract the complete app ZIP.", executable);
        string? argumentFile = null;
        try
        {
        if (Path.GetFileName(executable).Equals("ExifTool.exe", StringComparison.OrdinalIgnoreCase))
        {
            argumentFile = Path.Combine(Path.GetTempPath(), "metaremover-" + Guid.NewGuid().ToString("N") + ".args");
            await File.WriteAllLinesAsync(argumentFile, args, new UTF8Encoding(false), token);
            args = ["-charset", "filename=UTF8", "-@", argumentFile];
        }
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        info.Environment.Remove("LC_ALL"); info.Environment.Remove("LC_CTYPE"); info.Environment.Remove("LANG");
        foreach (var argument in args) info.ArgumentList.Add(argument);
        using var process = Start(info);
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync(token); }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(output, error);
            throw;
        }
        var result = new ToolResult(process.ExitCode, await output, await error);
        token.ThrowIfCancellationRequested();
        if (result.ExitCode != 0) throw new IOException(string.IsNullOrWhiteSpace(result.Error) ? result.Output.Trim() : result.Error.Trim());
        return result;
        }
        finally { if (argumentFile != null && File.Exists(argumentFile)) File.Delete(argumentFile); }
    }
    private static Process Start(ProcessStartInfo info)
    {
        try { return Process.Start(info) ?? throw new IOException("Unable to start processing tool."); }
        catch (System.ComponentModel.Win32Exception e) { throw new IOException("Unable to start a bundled processing tool: " + e.Message, e); }
    }
}
