using Dramaturge.Browsers;

namespace e2e.Infrastructure;

internal sealed record ClientLaunchSettings(BrowserKind Browser, bool Headless)
{
    internal static ClientLaunchSettings Read(string client, BrowserKind browser = BrowserKind.Chrome, bool headless = true)
        => Read(client, browser, headless, Environment.GetEnvironmentVariable);

    internal static ClientLaunchSettings Read(string client, BrowserKind browser, bool headless, Func<string, string?> environment)
    {
        var browserValue = environment($"E2E_{client}_BROWSER") ?? environment("DRAMATURGE_BROWSER");
        if (browserValue is not null &&
            (!Enum.TryParse(browserValue, ignoreCase: true, out browser) || !Enum.IsDefined(browser)))
            throw new InvalidOperationException($"Invalid browser '{browserValue}' for {client}. Expected chrome, firefox, edge, or safari.");

        var headlessValue = environment($"E2E_{client}_HEADLESS");
        if (headlessValue is not null)
            headless = ParseBoolean(headlessValue);
        else if (environment("DRAMATURGE_HEADED") is { } headed)
            headless = !ParseBoolean(headed);

        return new(browser, headless);
    }

    private static bool ParseBoolean(string value) => value.ToLowerInvariant() switch
    {
        "true" or "1" => true,
        "false" or "0" => false,
        _ => throw new InvalidOperationException($"Invalid launch boolean '{value}'. Expected true, false, 1, or 0."),
    };

    internal BrowserLauncherBuilder CreateLauncher()
    {
        var channelValue = Environment.GetEnvironmentVariable("DRAMATURGE_CHANNEL") ?? "stable";
        if (!Enum.TryParse<BrowserReleaseChannel>(channelValue, ignoreCase: true, out var channel) || !Enum.IsDefined(channel))
            throw new InvalidOperationException($"Invalid browser channel '{channelValue}'.");
        return BrowserLauncher.Configure(Browser).WithReleaseChannel(channel)
            .WithHeadlessOption(Headless)
            .WithSessionCapability("acceptInsecureCerts", true);
    }
}
