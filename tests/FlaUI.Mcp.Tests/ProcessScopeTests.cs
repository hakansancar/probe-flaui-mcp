using System.Diagnostics;
using System.Text.Json;
using PlaywrightWindows.Mcp;
using PlaywrightWindows.Mcp.Core;
using Xunit;

namespace FlaUI.Mcp.Tests;

public class ProcessScopeTests
{
    private static readonly Process Current = Process.GetCurrentProcess();
    private static readonly string MissingProcessId = int.MaxValue.ToString();

    [Theory]
    [InlineData("")]
    [InlineData("--pid 1234")]
    [InlineData("--app")]
    [InlineData("--app --pid 1234")]
    [InlineData("--app Bayi --pid")]
    [InlineData("--app Bayi --app Other")]
    [InlineData("--app Bayi --pid abc")]
    [InlineData("--app Bayi --pid 0")]
    [InlineData("--app Bayi --verbose 1")]
    [InlineData("--app C:\\Apps\\Bayi.exe")]
    [InlineData("--app .exe")]
    public void Parse_RejectsInvalidArguments(string commandLine)
    {
        Assert.Throws<ArgumentException>(() => ProcessScope.Parse(Split(commandLine)));
    }

    [Fact]
    public void Parse_DevelopmentMode()
    {
        var scope = ProcessScope.Parse(Split("--app Bayi.EXE"));

        Assert.Equal("Bayi", scope.AppName);
        Assert.False(scope.IsProductMode);
    }

    [Fact]
    public void Parse_ProductMode()
    {
        var scope = ProcessScope.Parse(Split("--app Bayi --pid 100 --pid 200"));

        Assert.True(scope.IsProductMode);
    }

    [Fact]
    public void Validate_AcceptsMatchingProcess()
    {
        var scope = ProcessScope.Parse(new[] { "--app", Current.ProcessName.ToUpperInvariant() + ".exe", "--pid", Current.Id.ToString() });

        Assert.Null(Record.Exception(() => scope.Validate()));
    }

    [Fact]
    public void Validate_RejectsNameMismatch()
    {
        var scope = ProcessScope.Parse(new[] { "--app", "Bayi", "--pid", Current.Id.ToString() });

        Assert.Throws<ArgumentException>(() => scope.Validate());
    }

    [Fact]
    public void Validate_RejectsMissingProcess()
    {
        var scope = ProcessScope.Parse(new[] { "--app", "Bayi", "--pid", MissingProcessId });

        Assert.Throws<ArgumentException>(() => scope.Validate());
    }

    [Fact]
    public void AllowsProcess_DevelopmentModeMatchesByName()
    {
        Assert.True(ProcessScope.Parse(new[] { "--app", Current.ProcessName }).AllowsProcess(Current.Id));
        Assert.False(ProcessScope.Parse(new[] { "--app", "Bayi" }).AllowsProcess(Current.Id));
    }

    [Fact]
    public void AllowsProcess_ProductModeRequiresGivenProcessId()
    {
        var scope = ProcessScope.Parse(new[] { "--app", Current.ProcessName, "--pid", MissingProcessId });
        Assert.False(scope.AllowsProcess(Current.Id));

        scope.AddLaunchedProcess(Current.Id);
        Assert.True(scope.AllowsProcess(Current.Id));
    }

    [Theory]
    [InlineData("Bayi.exe", true)]
    [InlineData("C:\\Program Files\\Bayi\\BAYI.EXE", true)]
    [InlineData("Bayi", false)]
    [InlineData("cmd.exe", false)]
    [InlineData("C:\\Tools\\Bayi.exe.lnk", false)]
    [InlineData("\\\\server\\share\\Bayi.exe", false)]
    [InlineData("https://example.com/Bayi.exe", false)]
    [InlineData("shell:AppsFolder\\Bayi_123!App", false)]
    public void AllowsLaunch_OnlyTargetExecutable(string appPath, bool expected)
    {
        var scope = ProcessScope.Parse(new[] { "--app", "Bayi" });

        Assert.Equal(expected, scope.AllowsLaunch(appPath));
    }

    [Fact]
    public void HasTargetExited_OnlyInProductModeWhenNoGivenProcessRuns()
    {
        Assert.False(ProcessScope.Parse(new[] { "--app", "Bayi" }).HasTargetExited());
        Assert.False(ProcessScope.Parse(new[] { "--app", Current.ProcessName, "--pid", Current.Id.ToString() }).HasTargetExited());
        Assert.True(ProcessScope.Parse(new[] { "--app", Current.ProcessName, "--pid", MissingProcessId }).HasTargetExited());
    }

    [Fact]
    public async Task ToolRegistry_RejectsCallsWhenTargetExited()
    {
        var scope = ProcessScope.Parse(new[] { "--app", Current.ProcessName, "--pid", MissingProcessId });
        var registry = new ToolRegistry(scope: scope);
        registry.RegisterTool(new OkTool());

        var result = await registry.ExecuteToolAsync("ok", arguments: null);

        Assert.True(result.IsError);
        Assert.Contains("is not running", result.Content[0].Text);
    }

    [Fact]
    public async Task ToolRegistry_AllowsCallsWhileTargetRuns()
    {
        var scope = ProcessScope.Parse(new[] { "--app", Current.ProcessName, "--pid", Current.Id.ToString() });
        var registry = new ToolRegistry(scope: scope);
        registry.RegisterTool(new OkTool());

        var result = await registry.ExecuteToolAsync("ok", arguments: null);

        Assert.NotEqual(true, result.IsError);
        Assert.Equal("ok", result.Content[0].Text);
    }

    private static string[] Split(string commandLine)
    {
        return commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private sealed class OkTool : ITool
    {
        public string Name => "ok";

        public McpTool GetDefinition() => new()
        {
            Name = Name,
            Description = "Succeeds",
            InputSchema = new { type = "object" }
        };

        public Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
        {
            return Task.FromResult(new McpToolResult
            {
                Content = new List<McpContent> { new() { Type = "text", Text = "ok" } }
            });
        }
    }
}
