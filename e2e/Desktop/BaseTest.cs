using Dramaturge;
using Dramaturge.Browsers;
using e2e.Infrastructure;

namespace e2e.Desktop;

public abstract class BaseTest : e2e.BaseTest
{
    private ManagedProcess? application;
    private string? profile;
    protected override ClientKind Client => ClientKind.Desktop;

    protected override async Task<BrowserGroup> LaunchAsync()
    {
        profile = Path.Combine(RepositoryRoot, "e2e", "TestResults", "desktop", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profile);
        // Electron writes its actual dynamic DevTools port here; no fixed port can collide.
        var electron = Path.Combine(RepositoryRoot, "node_modules", "electron", "dist",
            OperatingSystem.IsMacOS() ? "Electron.app/Contents/MacOS/Electron" : "electron");
        List<string> arguments = [Path.Combine(RepositoryRoot, "apps", "desktop", "build"), "--remote-debugging-port=0",
            $"--user-data-dir={profile}"];
        if (SetupFixture.Settings.Headless)
        {
            arguments.InsertRange(0, ["--auto-servernum", "--server-args=-screen 0 1280x720x24", electron]);
            electron = "xvfb-run";
        }
        application = ManagedProcess.Start(RepositoryRoot, Path.Combine(profile, "electron.log"), electron, arguments,
            new Dictionary<string, string> { ["BITWARDEN_APPDATA_DIR"] = profile,
                ["ELECTRON_IS_DEV"] = "0", ["ELECTRON_NO_UPDATER"] = "1" });
        var deadline = DateTime.UtcNow.AddMinutes(2);
        var activePort = Path.Combine(profile, "DevToolsActivePort");
        while (DateTime.UtcNow < deadline)
        {
            if (application.HasExited) throw new InvalidOperationException($"Electron exited. See {application.LogPath}");
            if (File.Exists(activePort))
            {
                var lines = await File.ReadAllLinesAsync(activePort);
                if (lines.Length >= 2 && int.TryParse(lines[0], out var port))
                    return await BrowserGroup.LaunchAsync(BrowserLauncher.Configure(BrowserKind.Chrome)
                        .ConnectToExisting(new Uri($"ws://127.0.0.1:{port}{lines[1]}")));
            }
            await Task.Delay(100);
        }
        throw new TimeoutException($"Electron did not expose DevTools. See {application.LogPath}");
    }

    protected override async Task<Page> OpenPageAsync()
    {
        var deadline = DateTime.UtcNow.AddMinutes(1);
        while (DateTime.UtcNow < deadline)
        {
            var page = Group.DefaultBrowser.Pages.FirstOrDefault(p =>
                p.Url.StartsWith("file:") || p.Url.StartsWith("bw-desktop-file:"));
            if (page is not null) return page;
            if (application!.HasExited) throw new InvalidOperationException($"Electron exited. See {application.LogPath}");
            await Task.Delay(100);
        }
        throw new TimeoutException("Electron did not open its main window.");
    }

    protected override async ValueTask StopApplicationAsync()
    {
        if (application is not null) { await application.DisposeAsync(); application = null; }
    }
}
