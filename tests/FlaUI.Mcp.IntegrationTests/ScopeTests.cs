using PlaywrightWindows.Mcp.Core;
using PlaywrightWindows.Mcp.Tools;

namespace FlaUI.Mcp.IntegrationTests;

[Collection("TestApps")]
public class ScopeTests
{
    private readonly TestAppFixture _fixture;

    public ScopeTests(TestAppFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void ListWindows_ShowsOnlyScopedApp()
    {
        var windows = _fixture.Session.ListWindows();

        Assert.Contains(windows, w => w.title == "FlaUI-MCP Test App");
        Assert.DoesNotContain(windows, w => w.title == "FlaUI-MCP WPF Test App");
        Assert.All(windows, w => Assert.Equal("WinFormsTestApp", w.processName));
    }

    [Fact]
    public void RegisterWindow_RejectsOtherApp()
    {
        Assert.Throws<Exception>(() => _fixture.Session.RegisterWindow(_fixture.GetWpfWindow()!));
    }

    [Fact]
    public async Task FocusByTitle_RejectsOtherApp()
    {
        var tool = new FocusWindowTool(_fixture.Session);

        var result = await _fixture.CallTool(tool, new { title = "FlaUI-MCP WPF Test App" });

        Assert.Contains("Window not found", result);
    }

    [Fact]
    public async Task Launch_RejectsOtherExecutable()
    {
        var tool = new LaunchTool(_fixture.Session);

        var result = await _fixture.CallTool(tool, new { app = "notepad.exe" });

        Assert.Contains("Only WinFormsTestApp.exe can be launched", result);
    }

    [Fact]
    public async Task Screenshot_RejectsFullScreen()
    {
        var tool = new ScreenshotTool(_fixture.Session, _fixture.Elements);

        var result = await _fixture.CallTool(tool, new { fullScreen = true });

        Assert.Contains("fullScreen capture is not allowed", result);
    }

    [Fact]
    public void ProductMode_AllowsOnlyGivenProcess()
    {
        var window = _fixture.GetWinFormsWindow()!;

        using var allowed = new SessionManager(ProcessScope.Parse(new[] { "--app", "WinFormsTestApp", "--pid", _fixture.WinFormsProcessId.ToString() }));
        Assert.NotEmpty(allowed.RegisterWindow(window));

        using var other = new SessionManager(ProcessScope.Parse(new[] { "--app", "WinFormsTestApp", "--pid", int.MaxValue.ToString() }));
        Assert.Throws<Exception>(() => other.RegisterWindow(window));
    }
}
