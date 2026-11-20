using PlaywrightWindows.Mcp;
using PlaywrightWindows.Mcp.Core;
using PlaywrightWindows.Mcp.Tools;

ProcessScope scope;
try
{
    scope = ProcessScope.Parse(args);
    scope.Validate();
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Console.Error.WriteLine("Usage: FlaUI.Mcp.exe --app <process name> [--pid <process id>]...");
    return 2;
}

DpiUtility.EnablePerMonitorV2();

// Create shared services
var sessionManager = new SessionManager(scope);
var elementRegistry = new ElementRegistry();

// Register all tools
var toolRegistry = new ToolRegistry(scope: scope);
toolRegistry.RegisterTool(new LaunchTool(sessionManager));
toolRegistry.RegisterTool(new SnapshotTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new ClickTool(elementRegistry));
toolRegistry.RegisterTool(new TypeTool(elementRegistry));
toolRegistry.RegisterTool(new FillTool(elementRegistry));
toolRegistry.RegisterTool(new GetTextTool(elementRegistry));
toolRegistry.RegisterTool(new SendKeysTool(elementRegistry));
toolRegistry.RegisterTool(new ScreenshotTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new ListWindowsTool(sessionManager));
toolRegistry.RegisterTool(new FocusWindowTool(sessionManager));
toolRegistry.RegisterTool(new CloseWindowTool(sessionManager));
toolRegistry.RegisterTool(new BatchTool(sessionManager, elementRegistry));

// Create and run MCP server
var server = new McpServer(toolRegistry);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    await server.RunAsync(cts.Token);
}
finally
{
    sessionManager.Dispose();
}

return 0;
