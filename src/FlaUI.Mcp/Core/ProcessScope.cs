using System.Diagnostics;
using FlaUI.Core.AutomationElements;

namespace PlaywrightWindows.Mcp.Core;

public class ProcessScope
{
    private readonly HashSet<int> _processIds;

    public ProcessScope(string appName, IEnumerable<int> processIds)
    {
        AppName = NormalizeName(appName);
        if (AppName.Length == 0 || AppName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException($"Invalid app name: {appName}");
        }

        _processIds = new HashSet<int>(processIds);
        IsProductMode = _processIds.Count > 0;
    }

    public string AppName { get; }

    public bool IsProductMode { get; }

    public static ProcessScope Parse(string[] args)
    {
        string? appName = null;
        var processIds = new List<int>();

        for (int i = 0; i < args.Length; i += 2)
        {
            var name = args[i];
            var value = i + 1 < args.Length ? args[i + 1] : null;

            if (name != "--app" && name != "--pid")
            {
                throw new ArgumentException($"Unknown argument: {name}");
            }

            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--"))
            {
                throw new ArgumentException($"Missing value for {name}");
            }

            if (name == "--app")
            {
                if (appName != null)
                {
                    throw new ArgumentException("--app can only be given once");
                }
                appName = value;
            }
            else
            {
                if (!int.TryParse(value, out var processId) || processId <= 0)
                {
                    throw new ArgumentException($"Invalid process id: {value}");
                }
                processIds.Add(processId);
            }
        }

        if (appName == null)
        {
            throw new ArgumentException("--app is required");
        }

        return new ProcessScope(appName, processIds);
    }

    public void Validate()
    {
        foreach (var processId in _processIds)
        {
            var processName = GetProcessName(processId);
            if (processName == null)
            {
                throw new ArgumentException($"Process {processId} is not running");
            }

            if (!IsAppName(processName))
            {
                throw new ArgumentException($"Process {processId} is {processName}, not {AppName}");
            }
        }
    }

    public bool Allows(AutomationElement element)
    {
        if (!element.Properties.ProcessId.TryGetValue(out var processId))
        {
            return false;
        }

        return AllowsProcess(processId);
    }

    public bool AllowsProcess(int processId)
    {
        if (IsProductMode && !_processIds.Contains(processId))
        {
            return false;
        }

        var processName = GetProcessName(processId);
        return processName != null && IsAppName(processName);
    }

    public bool AllowsLaunch(string appPath)
    {
        if (appPath.StartsWith(@"\\") || appPath.Contains("://"))
        {
            return false;
        }

        return string.Equals(Path.GetFileName(appPath), AppName + ".exe", StringComparison.OrdinalIgnoreCase);
    }

    public void AddLaunchedProcess(int processId)
    {
        _processIds.Add(processId);
    }

    public bool HasTargetExited()
    {
        if (!IsProductMode)
        {
            return false;
        }

        foreach (var processId in _processIds)
        {
            if (AllowsProcess(processId))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsAppName(string processName)
    {
        return string.Equals(NormalizeName(processName), AppName, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.Substring(0, trimmed.Length - 4);
        }

        return trimmed;
    }

    private static string? GetProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.HasExited ? null : process.ProcessName;
        }
        catch
        {
            return null;
        }
    }
}
