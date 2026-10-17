using System.Diagnostics;

namespace e2e.Infrastructure;

internal sealed class ManagedProcess : IAsyncDisposable
{
    private readonly Process process;
    private readonly Task output;
    private readonly Task errors;
    private readonly StreamWriter log;

    private ManagedProcess(Process process, StreamWriter log, string logPath)
    {
        this.process = process;
        this.log = log;
        LogPath = logPath;
        output = CopyAsync(process.StandardOutput);
        errors = CopyAsync(process.StandardError);
    }

    public string LogPath { get; }
    public bool HasExited => process.HasExited;

    public static ManagedProcess Start(string root, string logPath, string executable,
        IEnumerable<string> arguments, IReadOnlyDictionary<string, string>? environment = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        if (environment is not null)
            foreach (var (key, value) in environment) start.Environment[key] = value;
        var writer = new StreamWriter(logPath, append: false) { AutoFlush = true };
        try
        {
            return new ManagedProcess(Process.Start(start)
                ?? throw new InvalidOperationException($"Could not start {executable}"), writer, logPath);
        }
        catch
        {
            writer.Dispose();
            throw;
        }
    }

    public async Task<int> WaitAsync(TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
            await Task.WhenAll(output, errors);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"Process exceeded {timeout}. See {LogPath}");
        }
    }

    private async Task CopyAsync(StreamReader reader)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            // Both pipes share a writer; serialize writes without blocking pipe drainage.
            lock (log) log.WriteLine(line);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) when (process.HasExited) { }
            await process.WaitForExitAsync();
            await Task.WhenAll(output, errors);
        }
        finally
        {
            process.Dispose();
            await log.DisposeAsync();
        }
    }
}
