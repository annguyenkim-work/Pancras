using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace Pancras.Web.Tests;

/// <summary>
/// Một server Kestrel và một trình duyệt cho cả lớp smoke test (docs/02-coding-rules/testing.md mục 7). Mỗi test mở một browser context
/// riêng (cookie, cỡ màn hình riêng), nên các test không dính phiên đăng nhập của nhau mà không phải khởi động lại server và trình duyệt.
/// </summary>
public sealed class BrowserFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;

    public PancrasWebFactory Factory { get; } = new();

    public IBrowser Browser { get; private set; } = null!;

    public string BaseUrl { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        Factory.UseKestrel(0);
        Factory.StartServer();
        var addresses = Factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses;
        BaseUrl = addresses.First(address => address.StartsWith("http://", StringComparison.Ordinal)).TrimEnd('/');

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true, Channel = Channel() });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.DisposeAsync();
        }

        _playwright?.Dispose();
        await Factory.DisposeAsync();
    }

    /// <summary>Windows dùng Edge có sẵn; nơi khác dùng Chromium của Playwright (CI cài trước). Ghi đè bằng PANCRAS_BROWSER_CHANNEL.</summary>
    private static string? Channel()
    {
        var chosen = Environment.GetEnvironmentVariable("PANCRAS_BROWSER_CHANNEL");
        if (!string.IsNullOrWhiteSpace(chosen))
        {
            return chosen == "chromium" ? null : chosen;
        }

        return OperatingSystem.IsWindows() ? "msedge" : null;
    }
}
