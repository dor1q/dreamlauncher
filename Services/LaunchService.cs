using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using DreamLauncher.Models;

namespace DreamLauncher.Services;

public sealed class LaunchService
{
    private const uint CreateSuspended = 0x00000004;
    private const uint DetachedProcess = 0x00000008;

    private readonly DllInjectionService _dllInjectionService = new();

    private static readonly string[] GameProcesses =
    [
        "FortniteClient-Win64-Shipping_BE",
        "FortniteClient-Win64-Shipping_EAC",
        "FortniteClient-Win64-Shipping",
        "EpicGamesLauncher",
        "FortniteLauncher"
    ];

    private static readonly string[] BootstrapExecutables =
    [
        "FortniteLauncher.exe",
        "FortniteClient-Win64-Shipping_EAC.exe"
    ];

    public LaunchResult Launch(BuildDefinition build, LaunchContext context)
    {
        var executable = ResolveExecutable(build);

        if (!File.Exists(executable))
        {
            throw new FileNotFoundException("Executable not found", executable);
        }

        if (!string.Equals(Path.GetFileName(executable), BuildDefinition.DefaultExecutableFileName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Launch blocked: select {BuildDefinition.DefaultExecutableFileName}, not another launcher executable.");
        }

        var closedProcesses = build.CloseProcessesBeforeLaunch ? CloseGameProcesses() : 0;
        var bootstrapProcesses = build.StartBootstrapProcesses
            ? StartBootstrapProcesses(build.Path)
            : [];

        var info = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = Path.GetDirectoryName(executable)!,
            UseShellExecute = false
        };

        foreach (var argument in ResolveArguments(build, context))
        {
            info.ArgumentList.Add(argument);
        }

        foreach (var item in build.Env)
        {
            info.Environment[item.Key] = item.Value;
        }

        var process = Process.Start(info)
            ?? throw new InvalidOperationException("Game process could not be started.");
        string? injectedDll = null;

        if (build.ShouldInjectDll)
        {
            var dllPath = build.ResolvedDllPath
                ?? throw new InvalidOperationException("DLL injection is enabled, but DLL path is empty.");

            WaitBeforeDllInjection(process, build.DllInjectionDelayMs);
            _dllInjectionService.Inject(process, dllPath);
            injectedDll = dllPath;
        }

        return new LaunchResult
        {
            Executable = executable,
            ProcessId = process.Id,
            InjectedDll = injectedDll,
            ClosedProcessesBeforeLaunch = closedProcesses,
            StartedBootstrapProcesses = bootstrapProcesses
        };
    }

    public int CloseGameProcesses()
    {
        var closed = 0;

        foreach (var processName in GameProcesses)
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    process.Kill(true);
                    closed++;
                }
            }
        }

        return closed;
    }

    public bool IsGameRunning()
    {
        foreach (var processName in GameProcesses)
        {
            var processes = Process.GetProcessesByName(processName);
            var running = processes.Length > 0;

            foreach (var process in processes)
            {
                process.Dispose();
            }

            if (running)
            {
                return true;
            }
        }

        return false;
    }

    public void OpenInExplorer(string path)
    {
        var info = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{path}\"",
            UseShellExecute = true
        };

        Process.Start(info);
    }

    public static string ResolveExecutable(BuildDefinition build)
    {
        return build.ResolvedExecutable;
    }

    private static IEnumerable<string> ResolveArguments(BuildDefinition build, LaunchContext context)
    {
        var configured = build.Arguments.Count == 0
            ? BuildDefinition.DefaultArguments()
            : build.Arguments;
        var arguments = configured
            .Select(argument => ResolveArgument(argument, context))
            .Where(argument => !string.IsNullOrWhiteSpace(argument))
            .ToList();
        var knownKeys = arguments
            .Select(GetArgumentKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var insertIndex = 0;

        foreach (var required in BuildDefinition.DefaultArguments().Select(argument => ResolveArgument(argument, context)))
        {
            var key = GetArgumentKey(required);

            if (knownKeys.Add(key))
            {
                arguments.Insert(insertIndex, required);
                insertIndex++;
            }
        }

        return arguments;
    }

    private static string ResolveArgument(string argument, LaunchContext context)
    {
        return argument
            .Replace("{exchangeCode}", context.ExchangeCode, StringComparison.OrdinalIgnoreCase)
            .Replace("{accountId}", context.AccountId, StringComparison.OrdinalIgnoreCase)
            .Replace("{displayName}", context.DisplayName, StringComparison.OrdinalIgnoreCase)
            .Replace("{discordId}", context.DiscordId, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetArgumentKey(string argument)
    {
        var index = argument.IndexOf('=');
        return index <= 0 ? argument : argument[..index];
    }

    private static List<string> StartBootstrapProcesses(string buildRoot)
    {
        var started = new List<string>();
        var binariesPath = Path.Combine(buildRoot, "FortniteGame", "Binaries", "Win64");

        foreach (var fileName in BootstrapExecutables)
        {
            var path = Path.Combine(binariesPath, fileName);

            if (!File.Exists(path))
            {
                continue;
            }

            StartSuspended(path);
            started.Add(fileName);
        }

        return started;
    }

    private static void WaitBeforeDllInjection(Process process, int delayMs)
    {
        if (delayMs > 0 && process.WaitForExit(delayMs))
        {
            throw new InvalidOperationException($"Game process exited before DLL injection. Exit code: {process.ExitCode}.");
        }

        if (process.HasExited)
        {
            throw new InvalidOperationException($"Game process exited before DLL injection. Exit code: {process.ExitCode}.");
        }
    }

    private static void StartSuspended(string executable)
    {
        var workingDirectory = Path.GetDirectoryName(executable)
            ?? throw new InvalidOperationException($"Working directory could not be resolved for {executable}.");
        var startupInfo = new StartupInfo
        {
            Cb = Marshal.SizeOf<StartupInfo>()
        };
        var commandLine = new StringBuilder($"\"{executable}\"");

        if (!CreateProcessW(
                executable,
                commandLine,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                CreateSuspended | DetachedProcess,
                IntPtr.Zero,
                workingDirectory,
                ref startupInfo,
                out var processInformation))
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), $"Failed to start bootstrap process: {executable}");
        }

        CloseHandle(processInformation.ProcessHandle);
        CloseHandle(processInformation.ThreadHandle);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Cb;
        public string? Reserved;
        public string? Desktop;
        public string? Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public int Flags;
        public short ShowWindow;
        public short Reserved2;
        public IntPtr Reserved2Pointer;
        public IntPtr StdInput;
        public IntPtr StdOutput;
        public IntPtr StdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr ProcessHandle;
        public IntPtr ThreadHandle;
        public int ProcessId;
        public int ThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessW(
        string applicationName,
        StringBuilder commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string currentDirectory,
        ref StartupInfo startupInfo,
        out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
